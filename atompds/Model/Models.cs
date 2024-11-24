namespace atompds.Model;

public record ConfigRecord(
    int DbVersion,
    string PdsPfx,
    string PdsDid,
    string[] AvailableUserDomains,
    string BskyAppViewPfx,
    string BskyAppViewDid,
    string JwtAccessSecret);

public record AccountRecord(string Did,
    string Handle, 
    string Password, 
    string PrivateKey, 
    string PublicKey);