using System.Net;
using System.Net.Sockets;

namespace Account.Infrastructure;

public static class RequestSource
{
    public const string Unknown = "unknown";
    private const int MaxLength = 45;

    public static string FromRemoteAddress(IPAddress? address)
    {
        if (address is null)
        {
            return Unknown;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var text = address.ToString();
        return text.Length > MaxLength ? text[..MaxLength] : text;
    }
}