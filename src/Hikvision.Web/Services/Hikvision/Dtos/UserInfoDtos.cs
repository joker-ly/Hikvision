using System.Text.Json.Serialization;

namespace Hikvision.Web.Services.Hikvision.Dtos;

// نموذج بيانات ISAPI للبحث عن الأشخاص المسجّلين على الجهاز (UserInfo).

public class UserInfoSearchRequest
{
    [JsonPropertyName("UserInfoSearchCond")]
    public UserInfoSearchCond UserInfoSearchCond { get; set; } = new();
}

public class UserInfoSearchCond
{
    [JsonPropertyName("searchID")]
    public string SearchID { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("searchResultPosition")]
    public int SearchResultPosition { get; set; }

    [JsonPropertyName("maxResults")]
    public int MaxResults { get; set; } = 30;
}

public class UserInfoSearchResponse
{
    [JsonPropertyName("UserInfoSearch")]
    public UserInfoSearchResult? UserInfoSearch { get; set; }
}

public class UserInfoSearchResult
{
    [JsonPropertyName("searchID")]
    public string? SearchID { get; set; }

    [JsonPropertyName("responseStatusStrg")]
    public string? ResponseStatusStrg { get; set; }

    [JsonPropertyName("numOfMatches")]
    public int NumOfMatches { get; set; }

    [JsonPropertyName("totalMatches")]
    public int TotalMatches { get; set; }

    [JsonPropertyName("UserInfo")]
    public List<UserInfo> UserInfo { get; set; } = new();
}

public class UserInfo
{
    [JsonPropertyName("employeeNo")]
    public string? EmployeeNo { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("userType")]
    public string? UserType { get; set; }

    [JsonPropertyName("gender")]
    public string? Gender { get; set; }
}

/// <summary>شخص مستورد من الجهاز.</summary>
public record DeviceUser(string EmployeeNo, string? Name);
