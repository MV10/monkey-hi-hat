
namespace updateconf;

public class LinuxConstants
{
    private static readonly string linuxHomeDirectory = Environment.GetEnvironmentVariable("HOME");
    
    public static readonly string programPath = $"{linuxHomeDirectory}/monkeyhihat";
    public static readonly string contentPath = $"{linuxHomeDirectory}/mhh-content";

    public static readonly string log = "/tmp/mhh-update.log";
}

