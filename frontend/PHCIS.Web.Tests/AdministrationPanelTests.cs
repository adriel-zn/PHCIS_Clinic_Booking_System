using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PHCIS.Web.Components;
using PHCIS.Web.Models;
using PHCIS.Web.Services;

namespace PHCIS.Web.Tests;

public sealed class AdministrationPanelTests : BunitContext
{
    [Fact]
    public void GrantFormRequiresExplicitConfirmationAndExplainsNextLogin()
    {
        var writes = new List<string>();
        Configure(async request =>
        {
            writes.Add(await request.Content!.ReadAsStringAsync());
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var page = Render<AdministratorGrantPanel>();
        page.Find("#administrator-email").Change("second@example.test");
        page.Find("form").Submit();
        Assert.Empty(writes);
        page.Find("#confirm-administrator").Change(true);
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Contains("sign out and sign back in", page.Markup));
        var body = JsonDocument.Parse(Assert.Single(writes)).RootElement;
        Assert.True(body.GetProperty("confirmAdministratorAccess").GetBoolean());
        Assert.Equal("second@example.test", body.GetProperty("email").GetString());
    }

    [Fact]
    public void ClinicEditorLoadsSelectionAndSavesUpdatedDetails()
    {
        var id = Guid.NewGuid();
        string? updated = null;
        Configure(async request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                Assert.Equal($"/api/clinic-management/{id}/details", request.RequestUri!.AbsolutePath);
                updated = await request.Content!.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[] { new ClinicCard(id, "Original", "Original address", null) })
            };
        });
        var page = Render<ClinicAdministrationPanel>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("option").Count));
        page.Find("#managed-clinic").Change(id.ToString());
        Assert.Equal("Original", page.Find("#edit-clinic-name").GetAttribute("value"));
        page.Find("#edit-clinic-name").Change("Updated clinic");
        page.Find("#edit-clinic-form").Submit();
        page.WaitForAssertion(() => Assert.Contains("Clinic details saved", page.Markup));
        Assert.Contains("Updated clinic", updated);
    }

    [Fact]
    public void SlotPanelClosesAvailabilityAndKeepsBookedSlotsProtected()
    {
        var clinicId = Guid.NewGuid();
        var openId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var closed = false;
        Configure(async request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                Assert.Equal($"/api/clinic-management/{clinicId}/slots/{openId}/availability", request.RequestUri!.AbsolutePath);
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                closed = body.RootElement.GetProperty("isBlocked").GetBoolean();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[]
                {
                    new ManagedTimeSlot(openId, start, start.AddMinutes(30), closed, false, false),
                    new ManagedTimeSlot(Guid.NewGuid(), start.AddHours(1), start.AddHours(1.5), false, true, true)
                })
            };
        });
        var page = Render<SlotManagementPanel>(p => p.Add(x => x.ClinicId, clinicId));
        page.FindAll("button").Single(x => x.TextContent == "Load slots").Click();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("tbody tr").Count));
        var bookedRow = page.FindAll("tbody tr")[1];
        Assert.All(bookedRow.QuerySelectorAll("button"), button => Assert.True(button.HasAttribute("disabled")));
        page.FindAll("tbody tr")[0].QuerySelectorAll("button")[1].Click();
        page.WaitForAssertion(() => Assert.Contains("Slot closed", page.Markup));
        Assert.True(closed);
        Assert.Contains("Reopen", page.Markup);
    }

    [Fact]
    public async Task SlotClientEncodesWallClockTimesWithSouthAfricanOffset()
    {
        string? json = null;
        Configure(async request =>
        {
            json = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        await Services.GetRequiredService<PortalClient>().SaveSlotAsync(Guid.NewGuid(), null, new SlotScheduleModel
        {
            StartsAt = new DateTime(2026, 11, 1, 9, 0, 0),
            EndsAt = new DateTime(2026, 11, 1, 9, 30, 0)
        });
        using var parsed = JsonDocument.Parse(json!);
        var start = parsed.RootElement.GetProperty("startsAt").GetDateTimeOffset();
        Assert.Equal(TimeSpan.FromHours(2), start.Offset);
        Assert.Equal(9, start.Hour);
    }

    private void Configure(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
    {
        var http = new HttpClient(new StubHandler(respond)) { BaseAddress = new Uri("https://api.test/") };
        Services.AddSingleton(new PortalClient(http));
    }
}
