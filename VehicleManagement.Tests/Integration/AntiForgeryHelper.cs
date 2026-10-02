using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace VehicleManagement.Tests.Integration;

public static class AntiForgeryHelper
{
    public static async Task<string> GetTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/Manufacturers/Create");

        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();

        var match = Regex.Match(
            html,
            @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");

        if (!match.Success)
        {
            throw new InvalidOperationException(
                "Anti-forgery token was not found in the response.");
        }

        return match.Groups[1].Value;
    }
}