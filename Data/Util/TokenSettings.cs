namespace Data.Util
{
    public class TokenSettings
    {
        public required string SecretKey { get; set; }
        public required string ValidIssuer { get; set; }
        public required string ValidAudience { get; set; }
    }
}