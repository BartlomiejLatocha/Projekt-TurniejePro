using System.Text.Json.Serialization;

namespace TurniejePro.Contracts.Teams;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TeamRegistrationStatus
{
    Pending = 1,
    Accepted = 2,
    Rejected = 3
}
