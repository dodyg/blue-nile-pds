// ReSharper disable InconsistentNaming
// ReSharper disable PropertyCanBeMadeInitOnly.Global

using System.ComponentModel.DataAnnotations;

namespace atompds.Pds.Config;

public class ServerEnvironment
{
    // Service Configuration
    public string PDS_SERVICE_NAME { get; set; } = "AtomPDS";
    public int PDS_PORT { get; set; } = 2583;
    public string PDS_HOSTNAME { get; set; } = "localhost";
    public string? PDS_SERVICE_DID { get; set; }
    public string? PDS_VERSION { get; set; }
    public long PDS_BLOB_UPLOAD_LIMIT { get; set; } = 5 * 1024 * 1024; // 5MB
    public bool PDS_DEV_MODE { get; set; } = false;

    // Data Directories

    public string? PDS_DATA_DIRECTORY { get; set; }
    public string PDS_ACCOUNT_DB_LOCATION { get; set; } = "account.sqlite";
    public string PDS_SEQUENCER_DB_LOCATION { get; set; } = "sequencer.sqlite";
    public string PDS_DID_CACHE_DB_LOCATION { get; set; } = "did_cache.sqlite";

    // Actor Store
    public string PDS_ACTOR_STORE_DIRECTORY { get; set; } = "actors";
    public long PDS_ACTOR_SCORE_CACHE_SIZE { get; set; } = 100;

    // Blobstore
    [Required]
    public required string PDS_BLOBSTORE_DISK_LOCATION { get; set; }
    [Required]
    public required string PDS_BLOBSTORE_DISK_TMP_LOCATION { get; set; }

    // Identity
    private const int SECOND = 1000;
    private const int MINUTE = 60 * SECOND;
    private const int HOUR = 60 * MINUTE;
    private const int DAY = 24 * HOUR;
    public string PDS_DID_PLC_URL { get; set; } = "https://plc.directory";
    public int PDS_DID_CACHE_STALE_TTL { get; set; } = DAY;
    public int PDS_DID_CACHE_MAX_TTL { get; set; } = HOUR;
    public int PDS_ID_RESOLVER_TIMEOUT { get; set; }  = 3 * SECOND;
    public string? PDS_RECOVERY_DID_KEY { get; set; }
    public List<string> PDS_SERVICE_HANDLE_DOMAINS { get; set; } = [];
    public bool PDS_ENABLE_DID_DOC_WITH_SESSION { get; set; } = false;

    // Invites
    public bool PDS_INVITE_REQUIRED { get; set; } = false;
    public int? PDS_INVITE_INTERVAL { get; set; }
    public int InviteEpoch { get; set; } = 0;
    
    // Subscription
    public long PDS_MAX_SUBSCRIPTION_BUFFER { get; set; } = 500;
    public long PDS_REPO_BACKFILL_LIMIT_MS { get; set; } = DAY;

    // Bsky App View
    public string? PDS_BSKY_APP_VIEW_URL { get; set; }
    public string? PDS_BSKY_APP_VIEW_DID { get; set; }
    public string? PDS_BSKY_APP_VIEW_CDN_URL_PATTERN { get; set; }

    // Crawlers
    public List<string> PDS_CRAWLERS { get; set; } = [];

    // Secrets
    [Required]
    // openssl rand --hex 16
    public required string PDS_JWT_SECRET { get; set; }
    
    // Keys
    [Required]
    // openssl ecparam --name secp256k1 --genkey --noout --outform DER | tail --bytes=+8 | head --bytes=32 | xxd --plain --cols 32
    public required string PDS_PLC_ROTATION_KEY_K256_PRIVATE_KEY_HEX { get; set; }
    
    // Fetch
    public bool? PDS_DISABLE_SSRF_PROTECTION { get; set; }
    public long? PDS_FETCH_MAX_RESPONSE_SIZE { get; set; }

    // Proxy
    public bool? PDS_PROXY_ALLOW_HTTP2 { get; set; }
    public int? PDS_PROXY_HEADERS_TIMEOUT { get; set; }
    public int? PDS_PROXY_BODY_TIMEOUT { get; set; }
    public long? PDS_PROXY_MAX_RESPONSE_SIZE { get; set; }
    public int? PDS_PROXY_MAX_RETRIES { get; set; }
    public bool? PDS_PROXY_PREFER_COMPRESSED { get; set; }
}