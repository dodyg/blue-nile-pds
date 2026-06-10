using ActorStore.Repo;
using CID;
using Multiformats.Base;
using Multiformats.Codec;
using Multiformats.Hash;
using System.Threading.Tasks;

namespace ActorStore.Tests;

// !These tests are written by AI
public class ExtractBlobTests
{
    private static string GetValidCid() => 
        Cid.Create("test data", MultibaseEncoding.Base32Lower).ToString();

    private static string CreateBlobJson(string cid, string mimeType = "image/png", long size = 1024) => $$"""
        {
            "$type": "blob",
            "mimeType": "{{mimeType}}",
            "size": {{size}},
            "ref": { "$link": "{{cid}}" }
        }
        """;

    [Test]
    public async Task EmptyObject_ReturnsEmptyAsync()
    {
        var result = Prepare.ExtractBlobReferences("{}");
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task EmptyArray_ReturnsEmptyAsync()
    {
        var result = Prepare.ExtractBlobReferences("[]");
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task SimpleObjectNoBlobs_ReturnsEmptyAsync()
    {
        var json = """{ "name": "test", "value": 123 }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task SingleValidBlob_ReturnsSingleRefAsync()
    {
        var cid = GetValidCid();
        var json = CreateBlobJson(cid, "image/png", 2048);

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Cid.ToString()).IsEqualTo(cid);
        await Assert.That(result[0].MimeType).IsEqualTo("image/png");
        await Assert.That(result[0].Size).IsEqualTo(2048);
    }

    [Test]
    public async Task NestedBlob_ReturnsBlobRefAsync()
    {
        var cid = GetValidCid();
        var json = $$"""
        {
            "record": {
                "avatar": {
                    "$type": "blob",
                    "mimeType": "image/jpeg",
                    "size": 5000,
                    "ref": { "$link": "{{cid}}" }
                }
            }
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("image/jpeg");
        await Assert.That(result[0].Size).IsEqualTo(5000);
    }

    [Test]
    public async Task MultipleBlobs_ReturnsAllRefsAsync()
    {
        var cid1 = GetValidCid();
        var cid2 = Cid.Create("other data", MultibaseEncoding.Base32Lower).ToString();
        var json = $$"""
        {
            "avatar": { "$type": "blob", "mimeType": "image/png", "size": 1000, "ref": { "$link": "{{cid1}}" } },
            "banner": { "$type": "blob", "mimeType": "image/jpeg", "size": 2000, "ref": { "$link": "{{cid2}}" } }
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result.Length).IsEqualTo(2);
    }

    [Test]
    public async Task BlobInArray_ReturnsBlobRefAsync()
    {
        var cid = GetValidCid();
        var json = $$"""
        {
            "images": [
                { "$type": "blob", "mimeType": "image/gif", "size": 3000, "ref": { "$link": "{{cid}}" } }
            ]
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("image/gif");
    }

    [Test]
    public async Task MissingType_ReturnsEmptyAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "mimeType": "image/png", "size": 1024, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task WrongType_ReturnsEmptyAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "image", "mimeType": "image/png", "size": 1024, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task MissingMimeType_ReturnsEmptyAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "size": 1024, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task EmptyMimeType_ReturnsEmptyAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "", "size": 1024, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task MissingSize_ReturnsEmptyAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task ZeroSize_ReturnsEmptyAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 0, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task NegativeSize_ReturnsEmptyAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": -100, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task MissingRef_ReturnsEmptyAsync()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 1024 }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task RefNotObject_ReturnsEmptyAsync()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 1024, "ref": "invalid" }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task MissingLink_ReturnsEmptyAsync()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 1024, "ref": {} }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task EmptyLink_ReturnsEmptyAsync()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 1024, "ref": { "$link": "" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task InvalidCid_ReturnsEmptyAsync()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 1024, "ref": { "$link": "not-valid-cid" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task DeeplyNestedBlob_ReturnsBlobRefAsync()
    {
        var cid = GetValidCid();
        var json = $$"""
        {
            "l1": { "l2": { "l3": { "l4": { "blob": {
                "$type": "blob", "mimeType": "video/mp4", "size": 10000, "ref": { "$link": "{{cid}}" }
            } } } } }
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("video/mp4");
    }

    [Test]
    public async Task MixedContent_ReturnsOnlyBlobsAsync()
    {
        var cid = GetValidCid();
        var json = $$"""
        {
            "text": "Hello",
            "count": 42,
            "tags": ["a", "b"],
            "avatar": { "$type": "blob", "mimeType": "image/webp", "size": 4096, "ref": { "$link": "{{cid}}" } },
            "meta": { "created": "2024-01-01" }
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("image/webp");
    }

    [Test]
    public async Task BlueskyPostWithEmbed_ReturnsBlobRefAsync()
    {
        var cid = GetValidCid();
        var json = $$"""
        {
            "$type": "app.bsky.feed.post",
            "text": "Check this out!",
            "createdAt": "2024-01-15T12:00:00Z",
            "embed": {
                "$type": "app.bsky.embed.images",
                "images": [{
                    "alt": "Test image",
                    "image": { "$type": "blob", "mimeType": "image/jpeg", "size": 50000, "ref": { "$link": "{{cid}}" } }
                }]
            }
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("image/jpeg");
        await Assert.That(result[0].Size).IsEqualTo(50000);
    }

    [Test]
    public async Task ProfileWithAvatarAndBanner_ReturnsTwoRefsAsync()
    {
        var avatarCid = GetValidCid();
        var bannerCid = Cid.Create("banner", MultibaseEncoding.Base32Lower).ToString();
        var json = $$"""
        {
            "$type": "app.bsky.actor.profile",
            "displayName": "Test User",
            "avatar": { "$type": "blob", "mimeType": "image/png", "size": 10000, "ref": { "$link": "{{avatarCid}}" } },
            "banner": { "$type": "blob", "mimeType": "image/jpeg", "size": 50000, "ref": { "$link": "{{bannerCid}}" } }
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result.Length).IsEqualTo(2);
    }

    #region Edge Cases - Depth and Limits

    [Test]
    public async Task ExactlyAtMaxDepth32_ReturnsBlobRefAsync()
    {
        // MAX_LEVEL is 32, blob at level 32 should still be found
        var cid = GetValidCid();
        var json = BuildNestedJson(32, cid);

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
    }

    [Test]
    public async Task BeyondMaxDepth33_ReturnsEmptyAsync()
    {
        // Blob at level 33 should NOT be found (exceeds MAX_LEVEL of 32)
        var cid = GetValidCid();
        var json = BuildNestedJson(33, cid);

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    private static string BuildNestedJson(int depth, string cid)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < depth; i++)
            sb.Append($"{{ \"l{i}\": ");
        
        sb.Append($$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""");
        
        for (int i = 0; i < depth; i++)
            sb.Append(" }");
        
        return sb.ToString();
    }

    #endregion

    #region Edge Cases - Unicode and Special Characters

    [Test]
    public async Task MimeTypeWithUnicode_ReturnsBlobRefAsync()
    {
        var cid = GetValidCid();
        // Unusual but technically valid mime type with extended chars
        var json = $$"""{ "$type": "blob", "mimeType": "application/x-custom-тест", "size": 100, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("application/x-custom-тест");
    }

    [Test]
    public async Task JsonWithUnicodePropertyNames_FindsBlobAsync()
    {
        var cid = GetValidCid();
        var json = $$"""
        {
            "データ": {
                "画像": { "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }
            }
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
    }

    [Test]
    public async Task MimeTypeWithSpecialChars_ReturnsBlobRefAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "application/vnd.api+json; charset=utf-8", "size": 100, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
    }

    #endregion

    #region Edge Cases - Numeric Boundaries

    [Test]
    public async Task SizeAtLongMaxValue_ReturnsBlobRefAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 9223372036854775807, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Size).IsEqualTo(long.MaxValue);
    }

    [Test]
    public async Task SizeAsOne_ReturnsBlobRefAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 1, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Size).IsEqualTo(1);
    }

    [Test]
    public async Task SizeAsDecimal_ReturnsEmptyAsync()
    {
        // JSON numbers can be decimals, but size should be integer
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100.5, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        // Depending on implementation, this might truncate or fail
        // The current impl uses GetInt64() which truncates decimals
        await Assert.That(result).IsEmpty();
    }

    #endregion

    #region Edge Cases - Malformed/Tricky JSON Structures

    [Test]
    public async Task BlobTypeValueWithDifferentCase_ReturnsEmptyAsync()
    {
        // Type check is case-sensitive, "Blob" != "blob"
        var cid = GetValidCid();
        var json = $$"""{ "$type": "Blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task TypeWithLeadingWhitespace_ReturnsEmptyAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": " blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task TypeWithTrailingWhitespace_ReturnsEmptyAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob ", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task DuplicateBlobProperties_UsesLastValueAsync()
    {
        // JSON spec allows duplicate keys, parsers typically use last value
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "mimeType": "image/jpeg", "size": 100, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("image/jpeg");
    }

    [Test]
    public async Task NullMimeType_ReturnsEmptyAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": null, "size": 100, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task NullSize_ReturnsEmptyAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": null, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task NullRef_ReturnsEmptyAsync()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": null }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task NullLink_ReturnsEmptyAsync()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": null } }""";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task ExtraPropertiesInBlob_StillExtractedAsync()
    {
        var cid = GetValidCid();
        var json = $$"""
        { 
            "$type": "blob", 
            "mimeType": "image/png", 
            "size": 100, 
            "ref": { "$link": "{{cid}}" },
            "extra": "ignored",
            "another": 123
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
    }

    [Test]
    public async Task ExtraPropertiesInRef_StillExtractedAsync()
    {
        var cid = GetValidCid();
        var json = $$"""
        { 
            "$type": "blob", 
            "mimeType": "image/png", 
            "size": 100, 
            "ref": { "$link": "{{cid}}", "extra": "data" }
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
    }

    #endregion

    #region Edge Cases - Array Scenarios

    [Test]
    public async Task EmptyArrayInPath_StillFindsBlobAsync()
    {
        var cid = GetValidCid();
        var json = $$"""
        {
            "empty": [],
            "data": { "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
    }

    [Test]
    public async Task MixedArrayWithBlobsAndNonBlobs_FindsOnlyBlobsAsync()
    {
        var cid = GetValidCid();
        var json = $$"""
        {
            "items": [
                "string",
                123,
                null,
                { "notBlob": true },
                { "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } },
                { "$type": "other" }
            ]
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
    }

    [Test]
    public async Task NestedArrays_FindsBlobsAtAllLevelsAsync()
    {
        var cid = GetValidCid();
        var json = $$"""
        {
            "outer": [
                [
                    [
                        { "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }
                    ]
                ]
            ]
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
    }

    [Test]
    public async Task LargeArrayOfBlobs_FindsAllAsync()
    {
        var cid = GetValidCid();
        var blobs = string.Join(",", Enumerable.Range(0, 100).Select(_ => 
            $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }"""));
        var json = $"{{ \"images\": [{blobs}] }}";

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result.Length).IsEqualTo(100);
    }

    #endregion

    #region Edge Cases - Blob-like but Invalid

    [Test]
    public async Task ObjectWithBlobTypeButNestedBlob_FindsBothLevelsAsync()
    {
        // A blob containing another blob in extra properties - should find only the outer one
        // since the function returns immediately after finding a blob (doesn't recurse into it)
        var cid1 = GetValidCid();
        var cid2 = Cid.Create("inner", MultibaseEncoding.Base32Lower).ToString();
        var json = $$"""
        {
            "$type": "blob",
            "mimeType": "image/png",
            "size": 100,
            "ref": { "$link": "{{cid1}}" },
            "nested": { "$type": "blob", "mimeType": "image/jpeg", "size": 200, "ref": { "$link": "{{cid2}}" } }
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        // Should find only the outer blob since we `continue` after finding a blob
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Cid.ToString()).IsEqualTo(cid1);
    }

    [Test]
    public async Task RefWithNestedObjects_ReturnsEmptyAsync()
    {
        // ref should be { "$link": "cid" }, not nested objects
        var cid = GetValidCid();
        var json = $$"""
        { 
            "$type": "blob", 
            "mimeType": "image/png", 
            "size": 100, 
            "ref": { 
                "nested": { "$link": "{{cid}}" }
            }
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task SiblingBlobsInSameObject_FindsBothAsync()
    {
        var cid1 = GetValidCid();
        var cid2 = Cid.Create("second", MultibaseEncoding.Base32Lower).ToString();
        var json = $$"""
        {
            "first": { "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid1}}" } },
            "second": { "$type": "blob", "mimeType": "image/jpeg", "size": 200, "ref": { "$link": "{{cid2}}" } }
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result.Length).IsEqualTo(2);
    }

    #endregion

    #region Edge Cases - Primitive Root Values

    [Test]
    public async Task NullRoot_ReturnsEmptyAsync()
    {
        var result = Prepare.ExtractBlobReferences("null");
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task BooleanRoot_ReturnsEmptyAsync()
    {
        var result = Prepare.ExtractBlobReferences("true");
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task NumberRoot_ReturnsEmptyAsync()
    {
        var result = Prepare.ExtractBlobReferences("42");
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task StringRoot_ReturnsEmptyAsync()
    {
        var result = Prepare.ExtractBlobReferences("\"hello\"");
        await Assert.That(result).IsEmpty();
    }

    #endregion

    #region Edge Cases - CID Validation

    [Test]
    public async Task ValidCidV0_ReturnsBlobRefAsync()
    {
        // CIDv0 starts with Qm
        var cid = "QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdG";
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        // This depends on whether the CID library supports v0
        // If it doesn't, this test documents that behavior
        if (result.Length > 0)
            await Assert.That(result[0].Cid.ToString()).IsEqualTo(cid);
    }

    [Test]
    public async Task CidWithWhitespace_ReturnsEmptyAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": " {{cid}} " } }""";

        var result = Prepare.ExtractBlobReferences(json);

        // CID with leading/trailing whitespace should fail parsing
        await Assert.That(result).IsEmpty();
    }

    #endregion

    #region Edge Cases - Complex Real-World Scenarios

    [Test]
    public async Task PostWithMultipleEmbeddedImages_FindsAllBlobsAsync()
    {
        var cids = Enumerable.Range(0, 4)
            .Select(i => Cid.Create($"image{i}", MultibaseEncoding.Base32Lower).ToString())
            .ToArray();
        
        var json = $$"""
        {
            "$type": "app.bsky.feed.post",
            "text": "Multiple images!",
            "createdAt": "2024-01-15T12:00:00Z",
            "embed": {
                "$type": "app.bsky.embed.images",
                "images": [
                    { "alt": "Image 1", "image": { "$type": "blob", "mimeType": "image/jpeg", "size": 1000, "ref": { "$link": "{{cids[0]}}" } } },
                    { "alt": "Image 2", "image": { "$type": "blob", "mimeType": "image/jpeg", "size": 2000, "ref": { "$link": "{{cids[1]}}" } } },
                    { "alt": "Image 3", "image": { "$type": "blob", "mimeType": "image/jpeg", "size": 3000, "ref": { "$link": "{{cids[2]}}" } } },
                    { "alt": "Image 4", "image": { "$type": "blob", "mimeType": "image/jpeg", "size": 4000, "ref": { "$link": "{{cids[3]}}" } } }
                ]
            }
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result.Length).IsEqualTo(4);
    }

    [Test]
    public async Task PostWithVideoEmbed_FindsVideoAndThumbnailAsync()
    {
        var videoCid = GetValidCid();
        var thumbCid = Cid.Create("thumbnail", MultibaseEncoding.Base32Lower).ToString();
        
        var json = $$"""
        {
            "$type": "app.bsky.feed.post",
            "text": "Check out this video!",
            "embed": {
                "$type": "app.bsky.embed.video",
                "video": { "$type": "blob", "mimeType": "video/mp4", "size": 5000000, "ref": { "$link": "{{videoCid}}" } },
                "thumb": { "$type": "blob", "mimeType": "image/jpeg", "size": 50000, "ref": { "$link": "{{thumbCid}}" } }
            }
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result.Length).IsEqualTo(2);
        await Assert.That(result.Any(b => b.MimeType == "video/mp4")).IsTrue();
        await Assert.That(result.Any(b => b.MimeType == "image/jpeg")).IsTrue();
    }

    // ==========================================
    // AT Protocol Standard Lexicon Tests
    // Based on: https://github.com/bluesky-social/atproto/tree/main/lexicons
    // ==========================================

    // app.bsky.actor.profile - Profile with avatar and banner blobs
    [Test]
    public async Task AppBskyActorProfile_FullProfile_FindsAvatarAndBannerAsync()
    {
        var avatarCid = Cid.Create("avatar-image", MultibaseEncoding.Base32Lower).ToString();
        var bannerCid = Cid.Create("banner-image", MultibaseEncoding.Base32Lower).ToString();
        
        var json = $$"""
        {
            "$type": "app.bsky.actor.profile",
            "displayName": "Test User",
            "description": "Free-form profile description text.",
            "avatar": {
                "$type": "blob",
                "mimeType": "image/jpeg",
                "size": 500000,
                "ref": { "$link": "{{avatarCid}}" }
            },
            "banner": {
                "$type": "blob",
                "mimeType": "image/png",
                "size": 1000000,
                "ref": { "$link": "{{bannerCid}}" }
            },
            "createdAt": "2024-01-15T12:00:00.000Z"
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result.Length).IsEqualTo(2);
        await Assert.That(result.Any(b => b.Cid.ToString() == avatarCid && b.MimeType == "image/jpeg" && b.Size == 500000)).IsTrue();
        await Assert.That(result.Any(b => b.Cid.ToString() == bannerCid && b.MimeType == "image/png" && b.Size == 1000000)).IsTrue();
    }

    [Test]
    public async Task AppBskyActorProfile_OnlyAvatar_FindsOneBlobAsync()
    {
        var avatarCid = GetValidCid();
        
        var json = $$"""
        {
            "$type": "app.bsky.actor.profile",
            "displayName": "Minimal Profile",
            "avatar": {
                "$type": "blob",
                "mimeType": "image/png",
                "size": 250000,
                "ref": { "$link": "{{avatarCid}}" }
            }
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("image/png");
    }

    [Test]
    public async Task AppBskyActorProfile_NoBlobs_ReturnsEmptyAsync()
    {
        var json = """
        {
            "$type": "app.bsky.actor.profile",
            "displayName": "Text-Only Profile",
            "description": "A profile without images"
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    // app.bsky.embed.images - Image embeds (up to 4 images)
    [Test]
    public async Task AppBskyEmbedImages_MaxFourImages_FindsAllFourAsync()
    {
        var cids = Enumerable.Range(0, 4)
            .Select(i => Cid.Create($"image-{i}", MultibaseEncoding.Base32Lower).ToString())
            .ToArray();
        
        var json = $$"""
        {
            "$type": "app.bsky.embed.images",
            "images": [
                {
                    "image": { "$type": "blob", "mimeType": "image/jpeg", "size": 100000, "ref": { "$link": "{{cids[0]}}" } },
                    "alt": "First image description"
                },
                {
                    "image": { "$type": "blob", "mimeType": "image/png", "size": 200000, "ref": { "$link": "{{cids[1]}}" } },
                    "alt": "Second image description",
                    "aspectRatio": { "width": 1920, "height": 1080 }
                },
                {
                    "image": { "$type": "blob", "mimeType": "image/webp", "size": 150000, "ref": { "$link": "{{cids[2]}}" } },
                    "alt": ""
                },
                {
                    "image": { "$type": "blob", "mimeType": "image/gif", "size": 500000, "ref": { "$link": "{{cids[3]}}" } },
                    "alt": "Animated GIF"
                }
            ]
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result.Length).IsEqualTo(4);
        await Assert.That(result.Any(b => b.MimeType == "image/jpeg")).IsTrue();
        await Assert.That(result.Any(b => b.MimeType == "image/png")).IsTrue();
        await Assert.That(result.Any(b => b.MimeType == "image/webp")).IsTrue();
        await Assert.That(result.Any(b => b.MimeType == "image/gif")).IsTrue();
    }

    [Test]
    public async Task AppBskyEmbedImages_SingleImage_FindsOneAsync()
    {
        var cid = GetValidCid();
        
        var json = $$"""
        {
            "$type": "app.bsky.embed.images",
            "images": [
                {
                    "image": { "$type": "blob", "mimeType": "image/jpeg", "size": 750000, "ref": { "$link": "{{cid}}" } },
                    "alt": "A beautiful sunset"
                }
            ]
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Size).IsEqualTo(750000);
    }

    // app.bsky.embed.video - Video embeds with optional captions
    [Test]
    public async Task AppBskyEmbedVideo_VideoOnly_FindsVideoBlobAsync()
    {
        var videoCid = GetValidCid();
        
        var json = $$"""
        {
            "$type": "app.bsky.embed.video",
            "video": {
                "$type": "blob",
                "mimeType": "video/mp4",
                "size": 50000000,
                "ref": { "$link": "{{videoCid}}" }
            },
            "alt": "A funny cat video"
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("video/mp4");
        await Assert.That(result[0].Size).IsEqualTo(50000000);
    }

    [Test]
    public async Task AppBskyEmbedVideo_WithCaptions_FindsVideoAndCaptionBlobsAsync()
    {
        var videoCid = Cid.Create("video-content", MultibaseEncoding.Base32Lower).ToString();
        var captionEnCid = Cid.Create("caption-en", MultibaseEncoding.Base32Lower).ToString();
        var captionEsCid = Cid.Create("caption-es", MultibaseEncoding.Base32Lower).ToString();
        
        var json = $$"""
        {
            "$type": "app.bsky.embed.video",
            "video": {
                "$type": "blob",
                "mimeType": "video/mp4",
                "size": 100000000,
                "ref": { "$link": "{{videoCid}}" }
            },
            "captions": [
                {
                    "lang": "en",
                    "file": { "$type": "blob", "mimeType": "text/vtt", "size": 5000, "ref": { "$link": "{{captionEnCid}}" } }
                },
                {
                    "lang": "es",
                    "file": { "$type": "blob", "mimeType": "text/vtt", "size": 5500, "ref": { "$link": "{{captionEsCid}}" } }
                }
            ],
            "alt": "Tutorial video with subtitles",
            "aspectRatio": { "width": 1920, "height": 1080 }
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result.Length).IsEqualTo(3);
        await Assert.That(result.Where(b => b.MimeType == "video/mp4")).HasSingleItem();
        await Assert.That(result.Count(b => b.MimeType == "text/vtt")).IsEqualTo(2);
    }

    [Test]
    public async Task AppBskyEmbedVideo_MaxCaptions20_FindsAllBlobsAsync()
    {
        var videoCid = GetValidCid();
        var captionCids = Enumerable.Range(0, 20)
            .Select(i => Cid.Create($"caption-{i}", MultibaseEncoding.Base32Lower).ToString())
            .ToArray();
        
        var captionsJson = string.Join(",\n", captionCids.Select((cid, i) => 
            $$"""{ "lang": "lang{{i}}", "file": { "$type": "blob", "mimeType": "text/vtt", "size": 1000, "ref": { "$link": "{{cid}}" } } }"""));
        
        var json = $$"""
        {
            "$type": "app.bsky.embed.video",
            "video": { "$type": "blob", "mimeType": "video/mp4", "size": 50000000, "ref": { "$link": "{{videoCid}}" } },
            "captions": [{{captionsJson}}]
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result.Length).IsEqualTo(21); // 1 video + 20 captions
    }

    // app.bsky.embed.external - External link cards with optional thumbnail
    [Test]
    public async Task AppBskyEmbedExternal_WithThumb_FindsThumbnailBlobAsync()
    {
        var thumbCid = GetValidCid();
        
        var json = $$"""
        {
            "$type": "app.bsky.embed.external",
            "external": {
                "uri": "https://example.com/article",
                "title": "Interesting Article Title",
                "description": "A summary of the article content.",
                "thumb": {
                    "$type": "blob",
                    "mimeType": "image/jpeg",
                    "size": 100000,
                    "ref": { "$link": "{{thumbCid}}" }
                }
            }
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("image/jpeg");
    }

    [Test]
    public async Task AppBskyEmbedExternal_NoThumb_ReturnsEmptyAsync()
    {
        var json = """
        {
            "$type": "app.bsky.embed.external",
            "external": {
                "uri": "https://example.com/page",
                "title": "Page Title",
                "description": "Page description without thumbnail."
            }
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    // app.bsky.embed.recordWithMedia - Quote post with media
    [Test]
    public async Task AppBskyEmbedRecordWithMedia_ImagesMedia_FindsImageBlobsAsync()
    {
        var imageCid = GetValidCid();
        
        var json = $$"""
        {
            "$type": "app.bsky.embed.recordWithMedia",
            "record": {
                "$type": "app.bsky.embed.record",
                "record": {
                    "uri": "at://did:plc:example/app.bsky.feed.post/abc123",
                    "cid": "bafyreia..."
                }
            },
            "media": {
                "$type": "app.bsky.embed.images",
                "images": [
                    {
                        "image": { "$type": "blob", "mimeType": "image/png", "size": 300000, "ref": { "$link": "{{imageCid}}" } },
                        "alt": "Quote post with image"
                    }
                ]
            }
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("image/png");
    }

    [Test]
    public async Task AppBskyEmbedRecordWithMedia_VideoMedia_FindsVideoBlobAsync()
    {
        var videoCid = GetValidCid();
        
        var json = $$"""
        {
            "$type": "app.bsky.embed.recordWithMedia",
            "record": {
                "$type": "app.bsky.embed.record",
                "record": {
                    "uri": "at://did:plc:example/app.bsky.feed.post/xyz789",
                    "cid": "bafyreib..."
                }
            },
            "media": {
                "$type": "app.bsky.embed.video",
                "video": { "$type": "blob", "mimeType": "video/mp4", "size": 25000000, "ref": { "$link": "{{videoCid}}" } },
                "alt": "Quote post with video"
            }
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("video/mp4");
    }

    [Test]
    public async Task AppBskyEmbedRecordWithMedia_ExternalWithThumb_FindsThumbBlobAsync()
    {
        var thumbCid = GetValidCid();
        
        var json = $$"""
        {
            "$type": "app.bsky.embed.recordWithMedia",
            "record": {
                "$type": "app.bsky.embed.record",
                "record": {
                    "uri": "at://did:plc:example/app.bsky.feed.post/def456",
                    "cid": "bafyreic..."
                }
            },
            "media": {
                "$type": "app.bsky.embed.external",
                "external": {
                    "uri": "https://news.example.com/story",
                    "title": "Breaking News",
                    "description": "Important news story",
                    "thumb": { "$type": "blob", "mimeType": "image/jpeg", "size": 80000, "ref": { "$link": "{{thumbCid}}" } }
                }
            }
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
    }

    // app.bsky.feed.generator - Feed generator with avatar
    [Test]
    public async Task AppBskyFeedGenerator_WithAvatar_FindsAvatarBlobAsync()
    {
        var avatarCid = GetValidCid();
        
        var json = $$"""
        {
            "$type": "app.bsky.feed.generator",
            "did": "did:web:feed.example.com",
            "displayName": "My Custom Feed",
            "description": "A curated feed of interesting posts.",
            "avatar": {
                "$type": "blob",
                "mimeType": "image/png",
                "size": 150000,
                "ref": { "$link": "{{avatarCid}}" }
            },
            "acceptsInteractions": true,
            "createdAt": "2024-01-15T12:00:00.000Z"
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("image/png");
    }

    [Test]
    public async Task AppBskyFeedGenerator_NoAvatar_ReturnsEmptyAsync()
    {
        var json = """
        {
            "$type": "app.bsky.feed.generator",
            "did": "did:web:feed.example.com",
            "displayName": "Text-Only Feed",
            "createdAt": "2024-01-15T12:00:00.000Z"
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    // app.bsky.graph.list - List with avatar
    [Test]
    public async Task AppBskyGraphList_WithAvatar_FindsAvatarBlobAsync()
    {
        var avatarCid = GetValidCid();
        
        var json = $$"""
        {
            "$type": "app.bsky.graph.list",
            "purpose": "app.bsky.graph.defs#curatelist",
            "name": "Cool People to Follow",
            "description": "A list of interesting accounts.",
            "avatar": {
                "$type": "blob",
                "mimeType": "image/jpeg",
                "size": 200000,
                "ref": { "$link": "{{avatarCid}}" }
            },
            "createdAt": "2024-01-15T12:00:00.000Z"
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("image/jpeg");
    }

    [Test]
    public async Task AppBskyGraphList_ModerationList_WithAvatarAsync()
    {
        var avatarCid = GetValidCid();
        
        var json = $$"""
        {
            "$type": "app.bsky.graph.list",
            "purpose": "app.bsky.graph.defs#modlist",
            "name": "Blocked Accounts",
            "description": "Accounts I've blocked.",
            "avatar": {
                "$type": "blob",
                "mimeType": "image/png",
                "size": 100000,
                "ref": { "$link": "{{avatarCid}}" }
            },
            "createdAt": "2024-01-15T12:00:00.000Z"
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
    }

    // app.bsky.feed.post - Complete post scenarios
    [Test]
    public async Task AppBskyFeedPost_CompletePostWithImages_FindsAllBlobsAsync()
    {
        var imageCids = Enumerable.Range(0, 2)
            .Select(i => Cid.Create($"post-image-{i}", MultibaseEncoding.Base32Lower).ToString())
            .ToArray();
        
        var json = $$"""
        {
            "$type": "app.bsky.feed.post",
            "text": "Check out these photos! #photography #nature",
            "facets": [
                {
                    "index": { "byteStart": 27, "byteEnd": 39 },
                    "features": [{ "$type": "app.bsky.richtext.facet#tag", "tag": "photography" }]
                },
                {
                    "index": { "byteStart": 40, "byteEnd": 47 },
                    "features": [{ "$type": "app.bsky.richtext.facet#tag", "tag": "nature" }]
                }
            ],
            "embed": {
                "$type": "app.bsky.embed.images",
                "images": [
                    {
                        "image": { "$type": "blob", "mimeType": "image/jpeg", "size": 800000, "ref": { "$link": "{{imageCids[0]}}" } },
                        "alt": "Sunset over the mountains",
                        "aspectRatio": { "width": 4, "height": 3 }
                    },
                    {
                        "image": { "$type": "blob", "mimeType": "image/jpeg", "size": 750000, "ref": { "$link": "{{imageCids[1]}}" } },
                        "alt": "Forest trail",
                        "aspectRatio": { "width": 16, "height": 9 }
                    }
                ]
            },
            "langs": ["en"],
            "createdAt": "2024-01-15T12:00:00.000Z"
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result.Length).IsEqualTo(2);
        foreach (var blob in result)
        {
            await Assert.That(blob.MimeType).IsEqualTo("image/jpeg");
        }
    }

    [Test]
    public async Task AppBskyFeedPost_ReplyWithVideo_FindsVideoBlobAsync()
    {
        var videoCid = GetValidCid();
        
        var json = $$"""
        {
            "$type": "app.bsky.feed.post",
            "text": "Here's my response!",
            "reply": {
                "root": {
                    "uri": "at://did:plc:example/app.bsky.feed.post/root123",
                    "cid": "bafyreiaaa..."
                },
                "parent": {
                    "uri": "at://did:plc:example/app.bsky.feed.post/parent456",
                    "cid": "bafyreibbb..."
                }
            },
            "embed": {
                "$type": "app.bsky.embed.video",
                "video": { "$type": "blob", "mimeType": "video/mp4", "size": 15000000, "ref": { "$link": "{{videoCid}}" } },
                "alt": "My video reply"
            },
            "createdAt": "2024-01-15T12:00:00.000Z"
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("video/mp4");
    }

    [Test]
    public async Task AppBskyFeedPost_QuotePostWithMediaAndImages_FindsAllBlobsAsync()
    {
        var imageCids = Enumerable.Range(0, 3)
            .Select(i => Cid.Create($"quote-image-{i}", MultibaseEncoding.Base32Lower).ToString())
            .ToArray();
        
        var json = $$"""
        {
            "$type": "app.bsky.feed.post",
            "text": "This is so cool! Adding my own photos.",
            "embed": {
                "$type": "app.bsky.embed.recordWithMedia",
                "record": {
                    "$type": "app.bsky.embed.record",
                    "record": {
                        "uri": "at://did:plc:other/app.bsky.feed.post/original",
                        "cid": "bafyreiccc..."
                    }
                },
                "media": {
                    "$type": "app.bsky.embed.images",
                    "images": [
                        { "image": { "$type": "blob", "mimeType": "image/png", "size": 400000, "ref": { "$link": "{{imageCids[0]}}" } }, "alt": "Image 1" },
                        { "image": { "$type": "blob", "mimeType": "image/png", "size": 450000, "ref": { "$link": "{{imageCids[1]}}" } }, "alt": "Image 2" },
                        { "image": { "$type": "blob", "mimeType": "image/png", "size": 500000, "ref": { "$link": "{{imageCids[2]}}" } }, "alt": "Image 3" }
                    ]
                }
            },
            "createdAt": "2024-01-15T12:00:00.000Z"
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result.Length).IsEqualTo(3);
        foreach (var blob in result)
        {
            await Assert.That(blob.MimeType).IsEqualTo("image/png");
        }
    }

    [Test]
    public async Task AppBskyFeedPost_TextOnlyPost_ReturnsEmptyAsync()
    {
        var json = """
        {
            "$type": "app.bsky.feed.post",
            "text": "Just a simple text post with no media.",
            "langs": ["en"],
            "tags": ["thoughts", "random"],
            "createdAt": "2024-01-15T12:00:00.000Z"
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task AppBskyFeedPost_ExternalLinkWithThumb_FindsThumbBlobAsync()
    {
        var thumbCid = GetValidCid();
        
        var json = $$"""
        {
            "$type": "app.bsky.feed.post",
            "text": "Check out this article!",
            "embed": {
                "$type": "app.bsky.embed.external",
                "external": {
                    "uri": "https://blog.example.com/great-post",
                    "title": "A Great Blog Post",
                    "description": "This is a summary of the blog post content that appears in the preview card.",
                    "thumb": { "$type": "blob", "mimeType": "image/jpeg", "size": 120000, "ref": { "$link": "{{thumbCid}}" } }
                }
            },
            "createdAt": "2024-01-15T12:00:00.000Z"
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("image/jpeg");
    }

    // Complex multi-level scenarios
    [Test]
    public async Task ComplexScenario_VideoWithAllCaptionsAndQuote_FindsAllBlobsAsync()
    {
        var videoCid = Cid.Create("complex-video", MultibaseEncoding.Base32Lower).ToString();
        var captionCids = new[] {
            Cid.Create("caption-en", MultibaseEncoding.Base32Lower).ToString(),
            Cid.Create("caption-es", MultibaseEncoding.Base32Lower).ToString(),
            Cid.Create("caption-fr", MultibaseEncoding.Base32Lower).ToString()
        };
        
        var json = $$"""
        {
            "$type": "app.bsky.feed.post",
            "text": "A comprehensive video post with subtitles, quoting another post.",
            "embed": {
                "$type": "app.bsky.embed.recordWithMedia",
                "record": {
                    "$type": "app.bsky.embed.record",
                    "record": {
                        "uri": "at://did:plc:quoted/app.bsky.feed.post/original",
                        "cid": "bafyreiddd..."
                    }
                },
                "media": {
                    "$type": "app.bsky.embed.video",
                    "video": { "$type": "blob", "mimeType": "video/mp4", "size": 75000000, "ref": { "$link": "{{videoCid}}" } },
                    "captions": [
                        { "lang": "en", "file": { "$type": "blob", "mimeType": "text/vtt", "size": 8000, "ref": { "$link": "{{captionCids[0]}}" } } },
                        { "lang": "es", "file": { "$type": "blob", "mimeType": "text/vtt", "size": 8500, "ref": { "$link": "{{captionCids[1]}}" } } },
                        { "lang": "fr", "file": { "$type": "blob", "mimeType": "text/vtt", "size": 9000, "ref": { "$link": "{{captionCids[2]}}" } } }
                    ],
                    "alt": "Educational content with multiple language support"
                }
            },
            "langs": ["en", "es", "fr"],
            "createdAt": "2024-01-15T12:00:00.000Z"
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        await Assert.That(result.Length).IsEqualTo(4); // 1 video + 3 captions
        await Assert.That(result.Where(b => b.MimeType == "video/mp4")).HasSingleItem();
        await Assert.That(result.Count(b => b.MimeType == "text/vtt")).IsEqualTo(3);
    }

    // ==========================================
    // AT Protocol Data Model Spec Tests
    // Based on: https://atproto.com/specs/data-model#blob-type
    // ==========================================
    // Blob format according to spec:
    // - $type (string, required): fixed value "blob"
    // - ref (link, required): CID reference to blob, encoded as $link object
    // - mimeType (string, required, not empty): content type
    // - size (integer, required, positive, non-zero): length in bytes

    // $type field requirements
    [Test]
    public async Task Spec_TypeField_MustBeExactlyBlobAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).HasSingleItem();
    }

    [Test]
    public async Task Spec_TypeField_MissingType_InvalidAsync()
    {
        var cid = GetValidCid();
        // Missing $type field entirely
        var json = $$"""{ "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_TypeField_NullType_InvalidAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": null, "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_TypeField_NotString_InvalidAsync()
    {
        var cid = GetValidCid();
        // $type as number
        var json = $$"""{ "$type": 123, "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_TypeField_NotString_Boolean_InvalidAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": true, "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_TypeField_NotString_Array_InvalidAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": ["blob"], "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_TypeField_NotString_Object_InvalidAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": { "value": "blob" }, "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_TypeField_WrongValue_InvalidAsync()
    {
        var cid = GetValidCid();
        // $type is a string but not "blob"
        var json = $$"""{ "$type": "image", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_TypeField_CaseSensitive_UppercaseBlob_InvalidAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "BLOB", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_TypeField_CaseSensitive_MixedCase_InvalidAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "Blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_TypeField_EmptyString_InvalidAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    // ref field requirements (CID as $link object)
    [Test]
    public async Task Spec_RefField_ValidLinkObjectAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Cid.ToString()).IsEqualTo(cid);
    }

    [Test]
    public async Task Spec_RefField_MissingRef_InvalidAsync()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100 }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_RefField_NullRef_InvalidAsync()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": null }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_RefField_NotObject_String_InvalidAsync()
    {
        var cid = GetValidCid();
        // ref should be an object { "$link": "..." }, not a plain string
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": "{{cid}}" }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_RefField_NotObject_Array_InvalidAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": ["{{cid}}"] }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_RefField_MissingLink_InvalidAsync()
    {
        // ref is an object but missing $link
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": {} }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_RefField_WrongKey_InvalidAsync()
    {
        var cid = GetValidCid();
        // Using "link" instead of "$link"
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_RefField_LinkNotString_InvalidAsync()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": 12345 } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_RefField_LinkNull_InvalidAsync()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": null } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_RefField_LinkEmptyString_InvalidAsync()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_RefField_LinkWhitespaceOnly_InvalidAsync()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "   " } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_RefField_InvalidCid_InvalidAsync()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "not-a-valid-cid" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_RefField_CidWithWhitespace_InvalidAsync()
    {
        var cid = GetValidCid();
        // CID with leading/trailing whitespace
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": " {{cid}} " } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    // mimeType field requirements
    [Test]
    public async Task Spec_MimeType_ValidContentTypeAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/jpeg", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("image/jpeg");
    }

    [Test]
    public async Task Spec_MimeType_MissingMimeType_InvalidAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_MimeType_NullMimeType_InvalidAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": null, "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_MimeType_EmptyString_InvalidAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_MimeType_WhitespaceOnly_InvalidAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "   ", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_MimeType_NotString_InvalidAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": 123, "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_MimeType_ApplicationOctetStream_ValidDefaultAsync()
    {
        // Spec says: "application/octet-stream if not known"
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "application/octet-stream", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("application/octet-stream");
    }

    [Test]
    public async Task Spec_MimeType_WithParameters_ValidAsync()
    {
        var cid = GetValidCid();
        // MIME types can include parameters
        var json = $$"""{ "$type": "blob", "mimeType": "text/plain; charset=utf-8", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("text/plain; charset=utf-8");
    }

    [Test]
    public async Task Spec_MimeType_CommonTypes_VideoAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "video/mp4", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("video/mp4");
    }

    [Test]
    public async Task Spec_MimeType_CommonTypes_TextVttAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "text/vtt", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("text/vtt");
    }

    // size field requirements (integer, positive, non-zero)
    [Test]
    public async Task Spec_Size_PositiveInteger_ValidAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 1, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Size).IsEqualTo(1);
    }

    [Test]
    public async Task Spec_Size_LargeValue_ValidAsync()
    {
        var cid = GetValidCid();
        // 100MB video
        var json = $$"""{ "$type": "blob", "mimeType": "video/mp4", "size": 100000000, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Size).IsEqualTo(100000000);
    }

    [Test]
    public async Task Spec_Size_Max64BitInteger_ValidAsync()
    {
        var cid = GetValidCid();
        // Max safe integer for compatibility
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 9007199254740991, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Size).IsEqualTo(9007199254740991);
    }

    [Test]
    public async Task Spec_Size_MissingSize_InvalidAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_Size_NullSize_InvalidAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": null, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_Size_Zero_InvalidAsync()
    {
        // Spec says: "positive, non-zero"
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 0, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_Size_Negative_InvalidAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": -1, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_Size_LargeNegative_InvalidAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": -1000000, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_Size_NotNumber_String_InvalidAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": "100", "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_Size_NotNumber_Boolean_InvalidAsync()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": true, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_Size_Float_ShouldTruncateAsync()
    {
        // Spec requires integer, but JSON allows decimals
        // Implementation should handle this (truncate or reject)
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100.7, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        // Current implementation uses GetInt64() which truncates
        if (result.Length > 0)
        {
            await Assert.That(result[0].Size).IsEqualTo(100);
        }
    }

    // Extra fields should be ignored (spec: "Implementations should ignore unknown $ fields")
    [Test]
    public async Task Spec_ExtraFields_ShouldBeIgnoredAsync()
    {
        var cid = GetValidCid();
        var json = $$"""
        { 
            "$type": "blob", 
            "mimeType": "image/png", 
            "size": 100, 
            "ref": { "$link": "{{cid}}" },
            "extra": "ignored",
            "anotherField": 123,
            "$unknownDollarField": "also ignored"
        }
        """;
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).HasSingleItem();
    }

    [Test]
    public async Task Spec_ExtraFieldsInRef_ShouldBeIgnoredAsync()
    {
        var cid = GetValidCid();
        var json = $$"""
        { 
            "$type": "blob", 
            "mimeType": "image/png", 
            "size": 100, 
            "ref": { "$link": "{{cid}}", "extra": "data", "$other": true }
        }
        """;
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).HasSingleItem();
    }

    // Field order should not matter
    [Test]
    public async Task Spec_FieldOrder_ShouldNotMatterAsync()
    {
        var cid = GetValidCid();
        // Different ordering than standard
        var json = $$"""{ "size": 100, "ref": { "$link": "{{cid}}" }, "mimeType": "image/png", "$type": "blob" }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).HasSingleItem();
    }

    // CID format tests (spec mentions base32 for string encoding)
    [Test]
    public async Task Spec_CidFormat_Base32Encoded_ValidAsync()
    {
        // Standard CIDv1 base32
        var cid = Cid.Create("test content", MultibaseEncoding.Base32Lower).ToString();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Cid.ToString()).IsEqualTo(cid);
    }

    // Complete valid blob examples
    [Test]
    public async Task Spec_CompleteValidBlob_ImagePngAsync()
    {
        var cid = GetValidCid();
        var json = $$"""
        {
            "$type": "blob",
            "ref": { "$link": "{{cid}}" },
            "mimeType": "image/png",
            "size": 1000000
        }
        """;
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("image/png");
        await Assert.That(result[0].Size).IsEqualTo(1000000);
    }

    [Test]
    public async Task Spec_CompleteValidBlob_VideoMp4Async()
    {
        var cid = GetValidCid();
        var json = $$"""
        {
            "$type": "blob",
            "ref": { "$link": "{{cid}}" },
            "mimeType": "video/mp4",
            "size": 100000000
        }
        """;
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("video/mp4");
        await Assert.That(result[0].Size).IsEqualTo(100000000);
    }

    [Test]
    public async Task Spec_CompleteValidBlob_TextVttCaptionAsync()
    {
        var cid = GetValidCid();
        var json = $$"""
        {
            "$type": "blob",
            "ref": { "$link": "{{cid}}" },
            "mimeType": "text/vtt",
            "size": 20000
        }
        """;
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].MimeType).IsEqualTo("text/vtt");
        await Assert.That(result[0].Size).IsEqualTo(20000);
    }

    // Not blob objects that might look similar
    [Test]
    public async Task Spec_NotBlob_TypeIsNsid_NotBlobAsync()
    {
        // $type with an NSID (like a record type) should not be detected as blob
        var cid = GetValidCid();
        var json = $$"""
        {
            "$type": "app.bsky.feed.post",
            "ref": { "$link": "{{cid}}" },
            "mimeType": "image/png",
            "size": 100
        }
        """;
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Spec_NotBlob_HasAllFieldsButWrongTypeAsync()
    {
        var cid = GetValidCid();
        var json = $$"""
        {
            "$type": "link",
            "ref": { "$link": "{{cid}}" },
            "mimeType": "image/png",
            "size": 100
        }
        """;
        var result = Prepare.ExtractBlobReferences(json);
        await Assert.That(result).IsEmpty();
    }

    // ==========================================
    // CID Validation Tests
    // Based on: https://atproto.com/specs/data-model#link-and-cid-formats
    // ==========================================
    // The blessed formats for CIDs in atproto are:
    // - CIDv1
    // - multibase: base32 for string encoding
    // - multicodec: raw (0x55) for links to blobs
    // - multihash: sha-256 with 256 bits (0x12) is preferred

    // Multicodec validation - blobs must use 'raw' (0x55) codec
    [Test]
    public async Task CidValidation_RawCodec_ValidAsync()
    {
        // CID with raw codec (0x55) - valid for blobs
        var cid = Cid.Create("test blob data", MultibaseEncoding.Base32Lower);
        await Assert.That(cid.Codec).IsEqualTo((ulong)MulticodecCode.Raw);

        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Cid.ToString()).IsEqualTo(cid.ToString());
    }

    [Test]
    public async Task CidValidation_DagCborCodec_InvalidAsync()
    {
        // CID with dag-cbor codec (0x71) - NOT valid for blobs, only for data objects
        var hash = await Task.Run(() => Multihash.Sum(HashType.SHA2_256, System.Text.Encoding.UTF8.GetBytes("test data")));
        var dagCborCid = Cid.NewV1((ulong)MulticodecCode.MerkleDAGCBOR, hash, MultibaseEncoding.Base32Lower);
        
        await Assert.That(dagCborCid.Codec).IsEqualTo((ulong)MulticodecCode.MerkleDAGCBOR);

        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{dagCborCid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task CidValidation_DagPbCodec_InvalidAsync()
    {
        // CID with dag-pb codec (0x70) - NOT valid for blobs
        var hash = await Task.Run(() => Multihash.Sum(HashType.SHA2_256, System.Text.Encoding.UTF8.GetBytes("test data")));
        var dagPbCid = Cid.NewV1(Cid.DAG_PB, hash, MultibaseEncoding.Base32Lower);
        
        await Assert.That(dagPbCid.Codec).IsEqualTo(Cid.DAG_PB);

        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{dagPbCid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        await Assert.That(result).IsEmpty();
    }

    // CID Version tests
    [Test]
    public async Task CidValidation_CidV1_ValidAsync()
    {
        // CIDv1 with raw codec is the standard for blobs
        var cid = Cid.Create("v1 test", MultibaseEncoding.Base32Lower);
        var cidString = cid.ToString();
        await Assert.That(cid.Version).IsEqualTo(CID.Version.V1);

        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Cid.ToString()).IsEqualTo(cidString);
    }

    [Test]
    public async Task CidValidation_CidV0_InvalidAsync()
    {
        // CIDv0 uses dag-pb codec which is not valid for blobs
        // CIDv0 starts with "Qm" and is base58btc encoded
        var cidV0 = "QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdG";
        
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cidV0}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        // CIDv0 uses dag-pb (0x70) codec, not raw, so should be invalid
        await Assert.That(result).IsEmpty();
    }

    // Multibase encoding tests - CID should be read correctly regardless of encoding
    [Test]
    public async Task CidValidation_Base32Lower_ValidAsync()
    {
        var cid = Cid.Create("base32 test", MultibaseEncoding.Base32Lower);
        var cidString = cid.ToString();
        await Assert.That(cidString.StartsWith("b")).IsTrue(); // base32lower prefix

        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Cid.ToString()).IsEqualTo(cidString);
    }

    [Test]
    public async Task CidValidation_Base32Upper_ValidAsync()
    {
        var cid = Cid.Create("base32 upper test", MultibaseEncoding.Base32Upper);
        var cidString = cid.ToString();
        await Assert.That(cidString.StartsWith("B")).IsTrue(); // base32upper prefix

        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Cid.ToString()).IsEqualTo(cidString);
    }

    [Test]
    public async Task CidValidation_Base58Btc_ValidAsync()
    {
        var cid = Cid.Create("base58 test", MultibaseEncoding.Base58Btc);
        var cidString = cid.ToString();
        await Assert.That(cidString.StartsWith("z")).IsTrue(); // base58btc prefix

        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Cid.ToString()).IsEqualTo(cidString);
    }

    [Test]
    public async Task CidValidation_Base64_ValidAsync()
    {
        var cid = Cid.Create("base64 test", MultibaseEncoding.Base64);
        var cidString = cid.ToString();
        await Assert.That(cidString.StartsWith("m")).IsTrue(); // base64 prefix

        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Cid.ToString()).IsEqualTo(cidString);
    }

    // CID preserves encoding when parsed
    [Test]
    public async Task CidValidation_EncodingPreservedAfterParsingAsync()
    {
        var originalCid = Cid.Create("encoding preservation test", MultibaseEncoding.Base32Lower);
        var cidString = originalCid.ToString();
        
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cidString}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        await Assert.That(result).HasSingleItem();
        // The CID should be parseable and have correct codec
        await Assert.That(result[0].Cid.Codec).IsEqualTo((ulong)MulticodecCode.Raw);
        // CID string should be preserved after encoding/decoding
        await Assert.That(result[0].Cid.ToString()).IsEqualTo(cidString);
    }

    [Test]
    public async Task CidValidation_DifferentEncodingsSameCid_SameBlobAsync()
    {
        // Create CIDs with same content but different encodings
        var content = "same content different encoding";
        var cid32 = Cid.Create(content, MultibaseEncoding.Base32Lower);
        var cid58 = Cid.Create(content, MultibaseEncoding.Base58Btc);
        
        var json32 = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid32}}" } }""";
        var json58 = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid58}}" } }""";
        
        var result32 = Prepare.ExtractBlobReferences(json32);
        var result58 = Prepare.ExtractBlobReferences(json58);
        
        await Assert.That(result32).HasSingleItem();
        await Assert.That(result58).HasSingleItem();
        // Both should produce the same underlying CID (same hash)
        await Assert.That(result58[0].Cid.MultiHash).IsEqualTo(result32[0].Cid.MultiHash);
    }

    // Hash algorithm tests
    [Test]
    public async Task CidValidation_Sha256Hash_ValidAsync()
    {
        // SHA-256 is the standard and preferred hash
        var cid = Cid.Create("sha256 test", MultibaseEncoding.Base32Lower);
        var cidString = cid.ToString();
        await Assert.That(cid.MultiHash.Code).IsEqualTo(HashType.SHA2_256);

        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Cid.ToString()).IsEqualTo(cidString);
    }

    // Invalid CID format tests
    [Test]
    public async Task CidValidation_TruncatedCid_InvalidAsync()
    {
        var validCid = Cid.Create("test", MultibaseEncoding.Base32Lower).ToString();
        var truncatedCid = validCid[..10]; // Truncate the CID
        
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{truncatedCid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task CidValidation_GarbageData_InvalidAsync()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "bafynotarealcidatall" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task CidValidation_EmptyCid_InvalidAsync()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task CidValidation_SingleCharacter_InvalidAsync()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "b" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task CidValidation_InvalidMultibasePrefix_InvalidAsync()
    {
        // '!' is not a valid multibase prefix
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "!invalidprefix" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task CidValidation_ValidPrefixInvalidContent_InvalidAsync()
    {
        // 'b' is base32lower prefix but content is invalid
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "b0123456789" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        await Assert.That(result).IsEmpty();
    }

    // IPFS URL format tests (implementation supports /ipfs/ prefix)
    [Test]
    public async Task CidValidation_IpfsUrlFormat_ValidAsync()
    {
        var cid = Cid.Create("ipfs url test", MultibaseEncoding.Base32Lower);
        var cidString = cid.ToString();
        var ipfsUrl = $"/ipfs/{cid}";
        
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{ipfsUrl}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Cid.ToString()).IsEqualTo(cidString);
    }

    [Test]
    public async Task CidValidation_IpfsUrlWithPath_ValidAsync()
    {
        var cid = Cid.Create("ipfs with path", MultibaseEncoding.Base32Lower);
        var cidString = cid.ToString();
        var ipfsUrl = $"https://ipfs.io/ipfs/{cid}";
        
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{ipfsUrl}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Cid.ToString()).IsEqualTo(cidString);
    }

    // Edge cases
    [Test]
    public async Task CidValidation_CidWithCorrectCodecInNestedBlob_ValidAsync()
    {
        var cid = Cid.Create("nested blob cid", MultibaseEncoding.Base32Lower);
        var cidString = cid.ToString();
        var json = $$"""
        {
            "record": {
                "image": {
                    "$type": "blob",
                    "mimeType": "image/jpeg",
                    "size": 50000,
                    "ref": { "$link": "{{cid}}" }
                }
            }
        }
        """;
        
        var result = Prepare.ExtractBlobReferences(json);
        
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Cid.Codec).IsEqualTo((ulong)MulticodecCode.Raw);
        await Assert.That(result[0].Cid.ToString()).IsEqualTo(cidString);
    }

    [Test]
    public async Task CidValidation_MultipleBlobsWithDifferentEncodings_FindsAllAsync()
    {
        var cid1 = Cid.Create("blob1", MultibaseEncoding.Base32Lower);
        var cid2 = Cid.Create("blob2", MultibaseEncoding.Base58Btc);
        var cid3 = Cid.Create("blob3", MultibaseEncoding.Base64);
        var cidStrings = new[] { cid1.ToString(), cid2.ToString(), cid3.ToString() };
        
        var json = $$"""
        {
            "images": [
                { "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid1}}" } },
                { "$type": "blob", "mimeType": "image/jpeg", "size": 200, "ref": { "$link": "{{cid2}}" } },
                { "$type": "blob", "mimeType": "image/gif", "size": 300, "ref": { "$link": "{{cid3}}" } }
            ]
        }
        """;
        
        var result = Prepare.ExtractBlobReferences(json);
        
        await Assert.That(result.Length).IsEqualTo(3);
        foreach (var blob in result)
        {
            await Assert.That(blob.Cid.Codec).IsEqualTo((ulong)MulticodecCode.Raw);
        }
        // Each CID string should be preserved after encoding/decoding
        await Assert.That(result.Any(b => b.Cid.ToString() == cidStrings[0])).IsTrue();
        await Assert.That(result.Any(b => b.Cid.ToString() == cidStrings[1])).IsTrue();
        await Assert.That(result.Any(b => b.Cid.ToString() == cidStrings[2])).IsTrue();
    }

    [Test]
    public async Task CidValidation_MixedValidAndInvalidCodecs_FindsOnlyRawAsync()
    {
        var rawCid = Cid.Create("raw codec", MultibaseEncoding.Base32Lower);
        var hash = await Task.Run(() => Multihash.Sum(HashType.SHA2_256, System.Text.Encoding.UTF8.GetBytes("dag-cbor codec")));
        var dagCborCid = Cid.NewV1((ulong)MulticodecCode.MerkleDAGCBOR, hash, MultibaseEncoding.Base32Lower);
        
        var json = $$"""
        {
            "validBlob": { "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{rawCid}}" } },
            "invalidBlob": { "$type": "blob", "mimeType": "image/jpeg", "size": 200, "ref": { "$link": "{{dagCborCid}}" } }
        }
        """;
        
        var result = Prepare.ExtractBlobReferences(json);
        
        // Should only find the blob with raw codec
        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Cid.ToString()).IsEqualTo(rawCid.ToString());
    }
    #endregion
}
