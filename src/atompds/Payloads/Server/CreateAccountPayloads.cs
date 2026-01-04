using System;
using System.Text.Json.Serialization;

namespace atompds.Payloads.Server;

public class CreateAccountRequest
{

        public string? Email { get; set; }

        public required string Handle { get; set; }

        /// <summary>
        /// Pre-existing atproto DID, being imported to a new account.
        /// </summary>
        public string? Did { get; set; }

        public string? InviteCode { get; set; }

        public string? VerificationCode { get; set; }

        public string? VerificationPhone { get; set; }

        /// <summary>
        /// Initial account password. May need to meet instance-specific password strength requirements.
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// DID PLC rotation key (aka, recovery key) to be included in PLC creation operation.
        /// </summary>
        public string? RecoveryKey { get; set; }

        // /// <summary>
        // /// Gets or sets the plcOp.
        // /// <br/> A signed DID PLC operation to be submitted as part of importing an existing account to this instance. NOTE: this optional field may be updated when full account migration is implemented.
        // /// </summary>
        // [JsonPropertyName("plcOp")]
        // public ATObject? PlcOp { get; set; }

}
