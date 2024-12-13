using System.Text.RegularExpressions;
using Xrpc;

namespace CommonWeb;

public static partial class Util
{
    public static void EnsureValidDid(string did)
    {
        if (!did.StartsWith("did:"))
        {
            throw new Exception("DID requires \"did:\" prefix");
        }
        
        if (!BoringAscii().IsMatch(did))
        {
            throw new Exception("Disallowed characters in DID (ASCII letters, digits, and a couple other characters only)");
        }
        
        var items = did.Split(':');
        if (items.Length < 3)
        {
            throw new Exception("DID requires prefix, method, and method-specific content");
        }
        
        if (!MethodRegex().IsMatch(items[1]))
        {
            throw new Exception("Invalid method name");
        }

        if (did.EndsWith(':') || did.EndsWith('%'))
        {
            throw new Exception("DID cannot end with ':' or '%'");
        }
        
        if (did.Length > 2048)
        {
            throw new Exception("DID is too long (2048 chars max)");
        }
    }

    [GeneratedRegex(@"^[a-zA-Z0-9._:%-]*$")]
    private static partial Regex BoringAscii();
    
    [GeneratedRegex(@"^[a-z]+$")]
    private static partial Regex MethodRegex();

    public static void EnsureValidAtUri(string uri)
    {
        var uriParts = uri.Split('#');
        if (uriParts.Length > 2)
        {
            throw new Exception("URI cannot contain more than one '#' character");
        }
        
        var fragmentPart = uriParts.Length == 2 ? uriParts[1] : null;
        uri = uriParts[0];
        
        if (!UriRegex().IsMatch(uri))
        {
            throw new Exception("Disallowed characters in ATURI (ASCII)");
        }

        var parts = uri.Split('/');
        if (parts.Length >= 3 && (parts[0] != "at:" || parts[1].Length != 0))
        {
            throw new Exception("ATURI must start with \"at://");
        }
        if (parts.Length < 3)
        {
            throw new Exception("ATURI requires at least method and authority sections");
        }

        try
        {
            if (parts[2].StartsWith("did:"))
            {
                EnsureValidDid(parts[2]);
            }
            else
            {
                EnsureValidHandle(parts[2]);
            }
        }
        catch (Exception e)
        {
            throw new Exception("ATURI authority must be a valid handle or DID");
        }

        if (parts.Length >= 4)
        {
            if (parts[3].Length == 0)
            {
                throw new Exception("ATURI can not have a slash after authority without a path segment");
            }
            try
            {
                EnsureValidNSID(parts[3]);
            }
            catch (Exception)
            {
                throw new Exception("ATURI requires first path segment (if supplied) to be valid NSID");
            }
        }

        if (parts.Length >= 5)
        {
            if (parts[4].Length == 0)
            {
                throw new Exception("ATURI can not have a slash after collection, unless record key is provided");
            }
            // would validate rkey here, but there are basically no constraints!
        }
        
        if (parts.Length >= 6)
        {
            throw new Exception("ATURI path can have at most two parts, and no trailing slash");
        }
        
        if (uriParts.Length >= 2 && fragmentPart == null)
        {
            throw new Exception("ATURI fragment must be non-empty and start with slash");
        }
        
        if (fragmentPart != null)
        {
            if (fragmentPart.Length == 0 || fragmentPart[0] != '/')
            {
                throw new Exception("ATURI fragment must be non-empty and start with slash");
            }
            if (!Regex.IsMatch(fragmentPart, @"^\/[a-zA-Z0-9._~:@!$&')(*+,;=%[\]/-]*$"))
            {
                throw new Exception("Disallowed characters in ATURI fragment (ASCII)");
            }
        }
        
        if (uri.Length > 8 * 1024)
        {
            throw new Exception("ATURI is far too long");
        }
    }
    
    
    // Handle constraints, in English:
    //  - must be a possible domain name
    //    - RFC-1035 is commonly referenced, but has been updated. eg, RFC-3696,
    //      section 2. and RFC-3986, section 3. can now have leading numbers (eg,
    //      4chan.org)
    //    - "labels" (sub-names) are made of ASCII letters, digits, hyphens
    //    - can not start or end with a hyphen
    //    - TLD (last component) should not start with a digit
    //    - can't end with a hyphen (can end with digit)
    //    - each segment must be between 1 and 63 characters (not including any periods)
    //    - overall length can't be more than 253 characters
    //    - separated by (ASCII) periods; does not start or end with period
    //    - case insensitive
    //    - domains (handles) are equal if they are the same lower-case
    //    - punycode allowed for internationalization
    //  - no whitespace, null bytes, joining chars, etc
    //  - does not validate whether domain or TLD exists, or is a reserved or
    //    special TLD (eg, .onion or .local)
    //  - does not validate punycode
    public static void EnsureValidHandle(string handle)
    {
        if (!BasicHandleRegex().IsMatch(handle))
        {
            throw new XRPCError(new InvalidHandleErrorDetail("Disallowed characters in handle (ASCII letters, digits, dashes, periods only)"));
        }

        if (handle.Length > 253)
        {
            throw new XRPCError(new InvalidHandleErrorDetail("Handle is too long (253 chars max)"));
        }

        var labels = handle.Split('.');
        if (labels.Length < 2)
        {
            throw new XRPCError(new InvalidHandleErrorDetail("Handle domain needs at least two parts"));
        }

        for (var i = 0; i < labels.Length; i++)
        {
            var l = labels[i];
            if (l.Length < 1)
            {
                throw new XRPCError(new InvalidHandleErrorDetail("Handle parts can not be empty"));
            }
            if (l.Length > 63)
            {
                throw new XRPCError(new InvalidHandleErrorDetail("Handle part too long (max 63 chars)"));
            }
            if (l.EndsWith('-') || l.StartsWith('-'))
            {
                throw new XRPCError(new InvalidHandleErrorDetail("Handle parts can not start or end with hyphens"));
            }
            if (i + 1 == labels.Length && !Regex.IsMatch(l, "^[a-zA-Z]"))
            {
                throw new XRPCError(new InvalidHandleErrorDetail("Handle final component (TLD) must start with ASCII letter"));
            }
        }
    }

    public static void EnsureValidNSID(string nsid)
    {
        if (!Regex.IsMatch(nsid, "^[a-zA-Z0-9.-]*$"))
        {
            throw new Exception("Disallowed characters in NSID (ASCII letters, digits, dashes, periods only)");
        }
        
        if (nsid.Length > 317) // 253 + 1 + 63
        {
            throw new Exception("NSID is too long (317 chars max)");
        }
        
        var labels = nsid.Split('.');
        if (labels.Length < 3)
        {
            throw new Exception("NSID needs at least three parts");
        }
        
        for (var i = 0; i < labels.Length; i++)
        {
            var l = labels[i];
            if (l.Length < 1)
            {
                throw new Exception("NSID parts can not be empty");
            }
            if (l.Length > 63)
            {
                throw new Exception("NSID part too long (max 63 chars)");
            }
            if (l.EndsWith('-') || l.StartsWith('-'))
            {
                throw new Exception("NSID parts can not start or end with hyphen");
            }
            if (Regex.IsMatch(l, "^[0-9]") && i == 0)
            {
                throw new Exception("NSID first part may not start with a digit");
            }
            if (!Regex.IsMatch(l, "^[a-zA-Z]+$") && i + 1 == labels.Length)
            {
                throw new Exception("NSID name part must be only letters");
            }
        }
    }
    

    [GeneratedRegex("^[a-zA-Z0-9.-]*$")]
    private static partial Regex BasicHandleRegex();
    
    [GeneratedRegex(@"^[a-zA-Z0-9._~:@!$&')(*+,;=%/-]*$")]
    private static partial Regex UriRegex();
}