using System.Text.Json.Serialization;

namespace Hikvision.Web.Services.Hikvision.Dtos;

// نموذج بيانات بروتوكول ISAPI لأحداث التحكم بالدخول (AcsEvent).

public class AcsEventCondRequest
{
    [JsonPropertyName("AcsEventCond")]
    public AcsEventCond AcsEventCond { get; set; } = new();
}

public class AcsEventCond
{
    [JsonPropertyName("searchID")]
    public string SearchID { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("searchResultPosition")]
    public int SearchResultPosition { get; set; }

    [JsonPropertyName("maxResults")]
    public int MaxResults { get; set; } = 30;

    [JsonPropertyName("major")]
    public int Major { get; set; } = 5; // فئة أحداث التحكم بالدخول

    [JsonPropertyName("minor")]
    public int Minor { get; set; } = 0; // 0 = كل الأنواع

    [JsonPropertyName("startTime")]
    public string StartTime { get; set; } = string.Empty;

    [JsonPropertyName("endTime")]
    public string EndTime { get; set; } = string.Empty;
}

public class AcsEventResponse
{
    [JsonPropertyName("AcsEvent")]
    public AcsEventResult? AcsEvent { get; set; }
}

public class AcsEventResult
{
    [JsonPropertyName("searchID")]
    public string? SearchID { get; set; }

    [JsonPropertyName("totalMatches")]
    public int TotalMatches { get; set; }

    [JsonPropertyName("numOfMatches")]
    public int NumOfMatches { get; set; }

    [JsonPropertyName("responseStatusStrg")]
    public string? ResponseStatusStrg { get; set; }

    [JsonPropertyName("InfoList")]
    public List<AcsEventInfo> InfoList { get; set; } = new();
}

public class AcsEventInfo
{
    [JsonPropertyName("employeeNoString")]
    public string? EmployeeNoString { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("time")]
    public string? Time { get; set; }

    [JsonPropertyName("attendanceStatus")]
    public string? AttendanceStatus { get; set; }

    [JsonPropertyName("currentVerifyMode")]
    public string? CurrentVerifyMode { get; set; }

    [JsonPropertyName("pictureURL")]
    public string? PictureURL { get; set; }

    [JsonPropertyName("serialNo")]
    public long? SerialNo { get; set; }
}

// طلب/رد عدد الأحداث الكلي
public class AcsEventTotalNumResponse
{
    [JsonPropertyName("AcsEventTotalNum")]
    public AcsEventTotalNum? AcsEventTotalNum { get; set; }
}

public class AcsEventTotalNum
{
    [JsonPropertyName("searchID")]
    public string? SearchID { get; set; }

    [JsonPropertyName("totalNum")]
    public int TotalNum { get; set; }
}
