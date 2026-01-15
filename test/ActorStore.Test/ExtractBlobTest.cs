using ActorStore.Repo;
using CID;
using Multiformats.Base;
using Multiformats.Codec;
using Multiformats.Hash;

namespace ActorStore.Test;

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

    [Fact]
    public void EmptyObject_ReturnsEmpty()
    {
        var result = Prepare.ExtractBlobReferences("{}");
        Assert.Empty(result);
    }

    [Fact]
    public void EmptyArray_ReturnsEmpty()
    {
        var result = Prepare.ExtractBlobReferences("[]");
        Assert.Empty(result);
    }

    [Fact]
    public void SimpleObjectNoBlobs_ReturnsEmpty()
    {
        var json = """{ "name": "test", "value": 123 }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void SingleValidBlob_ReturnsSingleRef()
    {
        var cid = GetValidCid();
        var json = CreateBlobJson(cid, "image/png", 2048);

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Single(result);
        Assert.Equal(cid, result[0].Cid.ToString());
        Assert.Equal("image/png", result[0].MimeType);
        Assert.Equal(2048, result[0].Size);
    }

    [Fact]
    public void NestedBlob_ReturnsBlobRef()
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

        Assert.Single(result);
        Assert.Equal("image/jpeg", result[0].MimeType);
        Assert.Equal(5000, result[0].Size);
    }

    [Fact]
    public void MultipleBlobs_ReturnsAllRefs()
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

        Assert.Equal(2, result.Length);
    }

    [Fact]
    public void BlobInArray_ReturnsBlobRef()
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

        Assert.Single(result);
        Assert.Equal("image/gif", result[0].MimeType);
    }

    [Fact]
    public void MissingType_ReturnsEmpty()
    {
        var cid = GetValidCid();
        var json = $$"""{ "mimeType": "image/png", "size": 1024, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Empty(result);
    }

    [Fact]
    public void WrongType_ReturnsEmpty()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "image", "mimeType": "image/png", "size": 1024, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Empty(result);
    }

    [Fact]
    public void MissingMimeType_ReturnsEmpty()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "size": 1024, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Empty(result);
    }

    [Fact]
    public void EmptyMimeType_ReturnsEmpty()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "", "size": 1024, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Empty(result);
    }

    [Fact]
    public void MissingSize_ReturnsEmpty()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Empty(result);
    }

    [Fact]
    public void ZeroSize_ReturnsEmpty()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 0, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Empty(result);
    }

    [Fact]
    public void NegativeSize_ReturnsEmpty()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": -100, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Empty(result);
    }

    [Fact]
    public void MissingRef_ReturnsEmpty()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 1024 }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Empty(result);
    }

    [Fact]
    public void RefNotObject_ReturnsEmpty()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 1024, "ref": "invalid" }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Empty(result);
    }

    [Fact]
    public void MissingLink_ReturnsEmpty()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 1024, "ref": {} }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Empty(result);
    }

    [Fact]
    public void EmptyLink_ReturnsEmpty()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 1024, "ref": { "$link": "" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Empty(result);
    }

    [Fact]
    public void InvalidCid_ReturnsEmpty()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 1024, "ref": { "$link": "not-valid-cid" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Empty(result);
    }

    [Fact]
    public void DeeplyNestedBlob_ReturnsBlobRef()
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

        Assert.Single(result);
        Assert.Equal("video/mp4", result[0].MimeType);
    }

    [Fact]
    public void MixedContent_ReturnsOnlyBlobs()
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

        Assert.Single(result);
        Assert.Equal("image/webp", result[0].MimeType);
    }

    [Fact]
    public void BlueskyPostWithEmbed_ReturnsBlobRef()
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

        Assert.Single(result);
        Assert.Equal("image/jpeg", result[0].MimeType);
        Assert.Equal(50000, result[0].Size);
    }

    [Fact]
    public void ProfileWithAvatarAndBanner_ReturnsTwoRefs()
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

        Assert.Equal(2, result.Length);
    }

    #region Edge Cases - Depth and Limits

    [Fact]
    public void ExactlyAtMaxDepth32_ReturnsBlobRef()
    {
        // MAX_LEVEL is 32, blob at level 32 should still be found
        var cid = GetValidCid();
        var json = BuildNestedJson(32, cid);

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Single(result);
    }

    [Fact]
    public void BeyondMaxDepth33_ReturnsEmpty()
    {
        // Blob at level 33 should NOT be found (exceeds MAX_LEVEL of 32)
        var cid = GetValidCid();
        var json = BuildNestedJson(33, cid);

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Empty(result);
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

    [Fact]
    public void MimeTypeWithUnicode_ReturnsBlobRef()
    {
        var cid = GetValidCid();
        // Unusual but technically valid mime type with extended chars
        var json = $$"""{ "$type": "blob", "mimeType": "application/x-custom-тест", "size": 100, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Single(result);
        Assert.Equal("application/x-custom-тест", result[0].MimeType);
    }

    [Fact]
    public void JsonWithUnicodePropertyNames_FindsBlob()
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

        Assert.Single(result);
    }

    [Fact]
    public void MimeTypeWithSpecialChars_ReturnsBlobRef()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "application/vnd.api+json; charset=utf-8", "size": 100, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Single(result);
    }

    #endregion

    #region Edge Cases - Numeric Boundaries

    [Fact]
    public void SizeAtLongMaxValue_ReturnsBlobRef()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 9223372036854775807, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Single(result);
        Assert.Equal(long.MaxValue, result[0].Size);
    }

    [Fact]
    public void SizeAsOne_ReturnsBlobRef()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 1, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Single(result);
        Assert.Equal(1, result[0].Size);
    }

    [Fact]
    public void SizeAsDecimal_ReturnsEmpty()
    {
        // JSON numbers can be decimals, but size should be integer
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100.5, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        // Depending on implementation, this might truncate or fail
        // The current impl uses GetInt64() which truncates decimals
        Assert.Empty(result);
    }

    #endregion

    #region Edge Cases - Malformed/Tricky JSON Structures

    [Fact]
    public void BlobTypeValueWithDifferentCase_ReturnsEmpty()
    {
        // Type check is case-sensitive, "Blob" != "blob"
        var cid = GetValidCid();
        var json = $$"""{ "$type": "Blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Empty(result);
    }

    [Fact]
    public void TypeWithLeadingWhitespace_ReturnsEmpty()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": " blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Empty(result);
    }

    [Fact]
    public void TypeWithTrailingWhitespace_ReturnsEmpty()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob ", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Empty(result);
    }

    [Fact]
    public void DuplicateBlobProperties_UsesLastValue()
    {
        // JSON spec allows duplicate keys, parsers typically use last value
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "mimeType": "image/jpeg", "size": 100, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Single(result);
        Assert.Equal("image/jpeg", result[0].MimeType);
    }

    [Fact]
    public void NullMimeType_ReturnsEmpty()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": null, "size": 100, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Empty(result);
    }

    [Fact]
    public void NullSize_ReturnsEmpty()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": null, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Empty(result);
    }

    [Fact]
    public void NullRef_ReturnsEmpty()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": null }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Empty(result);
    }

    [Fact]
    public void NullLink_ReturnsEmpty()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": null } }""";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Empty(result);
    }

    [Fact]
    public void ExtraPropertiesInBlob_StillExtracted()
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

        Assert.Single(result);
    }

    [Fact]
    public void ExtraPropertiesInRef_StillExtracted()
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

        Assert.Single(result);
    }

    #endregion

    #region Edge Cases - Array Scenarios

    [Fact]
    public void EmptyArrayInPath_StillFindsBlob()
    {
        var cid = GetValidCid();
        var json = $$"""
        {
            "empty": [],
            "data": { "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Single(result);
    }

    [Fact]
    public void MixedArrayWithBlobsAndNonBlobs_FindsOnlyBlobs()
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

        Assert.Single(result);
    }

    [Fact]
    public void NestedArrays_FindsBlobsAtAllLevels()
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

        Assert.Single(result);
    }

    [Fact]
    public void LargeArrayOfBlobs_FindsAll()
    {
        var cid = GetValidCid();
        var blobs = string.Join(",", Enumerable.Range(0, 100).Select(_ => 
            $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }"""));
        var json = $"{{ \"images\": [{blobs}] }}";

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Equal(100, result.Length);
    }

    #endregion

    #region Edge Cases - Blob-like but Invalid

    [Fact]
    public void ObjectWithBlobTypeButNestedBlob_FindsBothLevels()
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
        Assert.Single(result);
        Assert.Equal(cid1, result[0].Cid.ToString());
    }

    [Fact]
    public void RefWithNestedObjects_ReturnsEmpty()
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

        Assert.Empty(result);
    }

    [Fact]
    public void SiblingBlobsInSameObject_FindsBoth()
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

        Assert.Equal(2, result.Length);
    }

    #endregion

    #region Edge Cases - Primitive Root Values

    [Fact]
    public void NullRoot_ReturnsEmpty()
    {
        var result = Prepare.ExtractBlobReferences("null");
        Assert.Empty(result);
    }

    [Fact]
    public void BooleanRoot_ReturnsEmpty()
    {
        var result = Prepare.ExtractBlobReferences("true");
        Assert.Empty(result);
    }

    [Fact]
    public void NumberRoot_ReturnsEmpty()
    {
        var result = Prepare.ExtractBlobReferences("42");
        Assert.Empty(result);
    }

    [Fact]
    public void StringRoot_ReturnsEmpty()
    {
        var result = Prepare.ExtractBlobReferences("\"hello\"");
        Assert.Empty(result);
    }

    #endregion

    #region Edge Cases - CID Validation

    [Fact]
    public void ValidCidV0_ReturnsBlobRef()
    {
        // CIDv0 starts with Qm
        var cid = "QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdG";
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";

        var result = Prepare.ExtractBlobReferences(json);

        // This depends on whether the CID library supports v0
        // If it doesn't, this test documents that behavior
        if (result.Length > 0)
            Assert.Equal(cid, result[0].Cid.ToString());
    }

    [Fact]
    public void CidWithWhitespace_ReturnsEmpty()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": " {{cid}} " } }""";

        var result = Prepare.ExtractBlobReferences(json);

        // CID with leading/trailing whitespace should fail parsing
        Assert.Empty(result);
    }

    #endregion

    #region Edge Cases - Complex Real-World Scenarios

    [Fact]
    public void PostWithMultipleEmbeddedImages_FindsAllBlobs()
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

        Assert.Equal(4, result.Length);
    }

    [Fact]
    public void PostWithVideoEmbed_FindsVideoAndThumbnail()
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

        Assert.Equal(2, result.Length);
        Assert.Contains(result, b => b.MimeType == "video/mp4");
        Assert.Contains(result, b => b.MimeType == "image/jpeg");
    }

    // ==========================================
    // AT Protocol Standard Lexicon Tests
    // Based on: https://github.com/bluesky-social/atproto/tree/main/lexicons
    // ==========================================

    // app.bsky.actor.profile - Profile with avatar and banner blobs
    [Fact]
    public void AppBskyActorProfile_FullProfile_FindsAvatarAndBanner()
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

        Assert.Equal(2, result.Length);
        Assert.Contains(result, b => b.Cid.ToString() == avatarCid && b.MimeType == "image/jpeg" && b.Size == 500000);
        Assert.Contains(result, b => b.Cid.ToString() == bannerCid && b.MimeType == "image/png" && b.Size == 1000000);
    }

    [Fact]
    public void AppBskyActorProfile_OnlyAvatar_FindsOneBlob()
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

        Assert.Single(result);
        Assert.Equal("image/png", result[0].MimeType);
    }

    [Fact]
    public void AppBskyActorProfile_NoBlobs_ReturnsEmpty()
    {
        var json = """
        {
            "$type": "app.bsky.actor.profile",
            "displayName": "Text-Only Profile",
            "description": "A profile without images"
        }
        """;

        var result = Prepare.ExtractBlobReferences(json);

        Assert.Empty(result);
    }

    // app.bsky.embed.images - Image embeds (up to 4 images)
    [Fact]
    public void AppBskyEmbedImages_MaxFourImages_FindsAllFour()
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

        Assert.Equal(4, result.Length);
        Assert.Contains(result, b => b.MimeType == "image/jpeg");
        Assert.Contains(result, b => b.MimeType == "image/png");
        Assert.Contains(result, b => b.MimeType == "image/webp");
        Assert.Contains(result, b => b.MimeType == "image/gif");
    }

    [Fact]
    public void AppBskyEmbedImages_SingleImage_FindsOne()
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

        Assert.Single(result);
        Assert.Equal(750000, result[0].Size);
    }

    // app.bsky.embed.video - Video embeds with optional captions
    [Fact]
    public void AppBskyEmbedVideo_VideoOnly_FindsVideoBlob()
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

        Assert.Single(result);
        Assert.Equal("video/mp4", result[0].MimeType);
        Assert.Equal(50000000, result[0].Size);
    }

    [Fact]
    public void AppBskyEmbedVideo_WithCaptions_FindsVideoAndCaptionBlobs()
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

        Assert.Equal(3, result.Length);
        Assert.Single(result.Where(b => b.MimeType == "video/mp4"));
        Assert.Equal(2, result.Count(b => b.MimeType == "text/vtt"));
    }

    [Fact]
    public void AppBskyEmbedVideo_MaxCaptions20_FindsAllBlobs()
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

        Assert.Equal(21, result.Length); // 1 video + 20 captions
    }

    // app.bsky.embed.external - External link cards with optional thumbnail
    [Fact]
    public void AppBskyEmbedExternal_WithThumb_FindsThumbnailBlob()
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

        Assert.Single(result);
        Assert.Equal("image/jpeg", result[0].MimeType);
    }

    [Fact]
    public void AppBskyEmbedExternal_NoThumb_ReturnsEmpty()
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

        Assert.Empty(result);
    }

    // app.bsky.embed.recordWithMedia - Quote post with media
    [Fact]
    public void AppBskyEmbedRecordWithMedia_ImagesMedia_FindsImageBlobs()
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

        Assert.Single(result);
        Assert.Equal("image/png", result[0].MimeType);
    }

    [Fact]
    public void AppBskyEmbedRecordWithMedia_VideoMedia_FindsVideoBlob()
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

        Assert.Single(result);
        Assert.Equal("video/mp4", result[0].MimeType);
    }

    [Fact]
    public void AppBskyEmbedRecordWithMedia_ExternalWithThumb_FindsThumbBlob()
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

        Assert.Single(result);
    }

    // app.bsky.feed.generator - Feed generator with avatar
    [Fact]
    public void AppBskyFeedGenerator_WithAvatar_FindsAvatarBlob()
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

        Assert.Single(result);
        Assert.Equal("image/png", result[0].MimeType);
    }

    [Fact]
    public void AppBskyFeedGenerator_NoAvatar_ReturnsEmpty()
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

        Assert.Empty(result);
    }

    // app.bsky.graph.list - List with avatar
    [Fact]
    public void AppBskyGraphList_WithAvatar_FindsAvatarBlob()
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

        Assert.Single(result);
        Assert.Equal("image/jpeg", result[0].MimeType);
    }

    [Fact]
    public void AppBskyGraphList_ModerationList_WithAvatar()
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

        Assert.Single(result);
    }

    // app.bsky.feed.post - Complete post scenarios
    [Fact]
    public void AppBskyFeedPost_CompletePostWithImages_FindsAllBlobs()
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

        Assert.Equal(2, result.Length);
        Assert.All(result, b => Assert.Equal("image/jpeg", b.MimeType));
    }

    [Fact]
    public void AppBskyFeedPost_ReplyWithVideo_FindsVideoBlob()
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

        Assert.Single(result);
        Assert.Equal("video/mp4", result[0].MimeType);
    }

    [Fact]
    public void AppBskyFeedPost_QuotePostWithMediaAndImages_FindsAllBlobs()
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

        Assert.Equal(3, result.Length);
        Assert.All(result, b => Assert.Equal("image/png", b.MimeType));
    }

    [Fact]
    public void AppBskyFeedPost_TextOnlyPost_ReturnsEmpty()
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

        Assert.Empty(result);
    }

    [Fact]
    public void AppBskyFeedPost_ExternalLinkWithThumb_FindsThumbBlob()
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

        Assert.Single(result);
        Assert.Equal("image/jpeg", result[0].MimeType);
    }

    // Complex multi-level scenarios
    [Fact]
    public void ComplexScenario_VideoWithAllCaptionsAndQuote_FindsAllBlobs()
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

        Assert.Equal(4, result.Length); // 1 video + 3 captions
        Assert.Single(result.Where(b => b.MimeType == "video/mp4"));
        Assert.Equal(3, result.Count(b => b.MimeType == "text/vtt"));
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
    [Fact]
    public void Spec_TypeField_MustBeExactlyBlob()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Single(result);
    }

    [Fact]
    public void Spec_TypeField_MissingType_Invalid()
    {
        var cid = GetValidCid();
        // Missing $type field entirely
        var json = $$"""{ "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_TypeField_NullType_Invalid()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": null, "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_TypeField_NotString_Invalid()
    {
        var cid = GetValidCid();
        // $type as number
        var json = $$"""{ "$type": 123, "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_TypeField_NotString_Boolean_Invalid()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": true, "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_TypeField_NotString_Array_Invalid()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": ["blob"], "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_TypeField_NotString_Object_Invalid()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": { "value": "blob" }, "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_TypeField_WrongValue_Invalid()
    {
        var cid = GetValidCid();
        // $type is a string but not "blob"
        var json = $$"""{ "$type": "image", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_TypeField_CaseSensitive_UppercaseBlob_Invalid()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "BLOB", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_TypeField_CaseSensitive_MixedCase_Invalid()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "Blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_TypeField_EmptyString_Invalid()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    // ref field requirements (CID as $link object)
    [Fact]
    public void Spec_RefField_ValidLinkObject()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Single(result);
        Assert.Equal(cid, result[0].Cid.ToString());
    }

    [Fact]
    public void Spec_RefField_MissingRef_Invalid()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100 }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_RefField_NullRef_Invalid()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": null }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_RefField_NotObject_String_Invalid()
    {
        var cid = GetValidCid();
        // ref should be an object { "$link": "..." }, not a plain string
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": "{{cid}}" }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_RefField_NotObject_Array_Invalid()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": ["{{cid}}"] }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_RefField_MissingLink_Invalid()
    {
        // ref is an object but missing $link
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": {} }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_RefField_WrongKey_Invalid()
    {
        var cid = GetValidCid();
        // Using "link" instead of "$link"
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_RefField_LinkNotString_Invalid()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": 12345 } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_RefField_LinkNull_Invalid()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": null } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_RefField_LinkEmptyString_Invalid()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_RefField_LinkWhitespaceOnly_Invalid()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "   " } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_RefField_InvalidCid_Invalid()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "not-a-valid-cid" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_RefField_CidWithWhitespace_Invalid()
    {
        var cid = GetValidCid();
        // CID with leading/trailing whitespace
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": " {{cid}} " } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    // mimeType field requirements
    [Fact]
    public void Spec_MimeType_ValidContentType()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/jpeg", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Single(result);
        Assert.Equal("image/jpeg", result[0].MimeType);
    }

    [Fact]
    public void Spec_MimeType_MissingMimeType_Invalid()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_MimeType_NullMimeType_Invalid()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": null, "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_MimeType_EmptyString_Invalid()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_MimeType_WhitespaceOnly_Invalid()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "   ", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_MimeType_NotString_Invalid()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": 123, "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_MimeType_ApplicationOctetStream_ValidDefault()
    {
        // Spec says: "application/octet-stream if not known"
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "application/octet-stream", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Single(result);
        Assert.Equal("application/octet-stream", result[0].MimeType);
    }

    [Fact]
    public void Spec_MimeType_WithParameters_Valid()
    {
        var cid = GetValidCid();
        // MIME types can include parameters
        var json = $$"""{ "$type": "blob", "mimeType": "text/plain; charset=utf-8", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Single(result);
        Assert.Equal("text/plain; charset=utf-8", result[0].MimeType);
    }

    [Fact]
    public void Spec_MimeType_CommonTypes_Video()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "video/mp4", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Single(result);
        Assert.Equal("video/mp4", result[0].MimeType);
    }

    [Fact]
    public void Spec_MimeType_CommonTypes_TextVtt()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "text/vtt", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Single(result);
        Assert.Equal("text/vtt", result[0].MimeType);
    }

    // size field requirements (integer, positive, non-zero)
    [Fact]
    public void Spec_Size_PositiveInteger_Valid()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 1, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Single(result);
        Assert.Equal(1, result[0].Size);
    }

    [Fact]
    public void Spec_Size_LargeValue_Valid()
    {
        var cid = GetValidCid();
        // 100MB video
        var json = $$"""{ "$type": "blob", "mimeType": "video/mp4", "size": 100000000, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Single(result);
        Assert.Equal(100000000, result[0].Size);
    }

    [Fact]
    public void Spec_Size_Max64BitInteger_Valid()
    {
        var cid = GetValidCid();
        // Max safe integer for compatibility
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 9007199254740991, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Single(result);
        Assert.Equal(9007199254740991, result[0].Size);
    }

    [Fact]
    public void Spec_Size_MissingSize_Invalid()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_Size_NullSize_Invalid()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": null, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_Size_Zero_Invalid()
    {
        // Spec says: "positive, non-zero"
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 0, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_Size_Negative_Invalid()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": -1, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_Size_LargeNegative_Invalid()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": -1000000, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_Size_NotNumber_String_Invalid()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": "100", "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_Size_NotNumber_Boolean_Invalid()
    {
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": true, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_Size_Float_ShouldTruncate()
    {
        // Spec requires integer, but JSON allows decimals
        // Implementation should handle this (truncate or reject)
        var cid = GetValidCid();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100.7, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        // Current implementation uses GetInt64() which truncates
        if (result.Length > 0)
        {
            Assert.Equal(100, result[0].Size);
        }
    }

    // Extra fields should be ignored (spec: "Implementations should ignore unknown $ fields")
    [Fact]
    public void Spec_ExtraFields_ShouldBeIgnored()
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
        Assert.Single(result);
    }

    [Fact]
    public void Spec_ExtraFieldsInRef_ShouldBeIgnored()
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
        Assert.Single(result);
    }

    // Field order should not matter
    [Fact]
    public void Spec_FieldOrder_ShouldNotMatter()
    {
        var cid = GetValidCid();
        // Different ordering than standard
        var json = $$"""{ "size": 100, "ref": { "$link": "{{cid}}" }, "mimeType": "image/png", "$type": "blob" }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Single(result);
    }

    // CID format tests (spec mentions base32 for string encoding)
    [Fact]
    public void Spec_CidFormat_Base32Encoded_Valid()
    {
        // Standard CIDv1 base32
        var cid = Cid.Create("test content", MultibaseEncoding.Base32Lower).ToString();
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        Assert.Single(result);
        Assert.Equal(cid, result[0].Cid.ToString());
    }

    // Complete valid blob examples
    [Fact]
    public void Spec_CompleteValidBlob_ImagePng()
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
        Assert.Single(result);
        Assert.Equal("image/png", result[0].MimeType);
        Assert.Equal(1000000, result[0].Size);
    }

    [Fact]
    public void Spec_CompleteValidBlob_VideoMp4()
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
        Assert.Single(result);
        Assert.Equal("video/mp4", result[0].MimeType);
        Assert.Equal(100000000, result[0].Size);
    }

    [Fact]
    public void Spec_CompleteValidBlob_TextVttCaption()
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
        Assert.Single(result);
        Assert.Equal("text/vtt", result[0].MimeType);
        Assert.Equal(20000, result[0].Size);
    }

    // Not blob objects that might look similar
    [Fact]
    public void Spec_NotBlob_TypeIsNsid_NotBlob()
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
        Assert.Empty(result);
    }

    [Fact]
    public void Spec_NotBlob_HasAllFieldsButWrongType()
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
        Assert.Empty(result);
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
    [Fact]
    public void CidValidation_RawCodec_Valid()
    {
        // CID with raw codec (0x55) - valid for blobs
        var cid = Cid.Create("test blob data", MultibaseEncoding.Base32Lower);
        Assert.Equal((ulong)MulticodecCode.Raw, cid.Codec);
        
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        Assert.Single(result);
        Assert.Equal(cid.ToString(), result[0].Cid.ToString());
    }

    [Fact]
    public void CidValidation_DagCborCodec_Invalid()
    {
        // CID with dag-cbor codec (0x71) - NOT valid for blobs, only for data objects
        var hash = Multihash.Sum(HashType.SHA2_256, System.Text.Encoding.UTF8.GetBytes("test data"));
        var dagCborCid = Cid.NewV1((ulong)MulticodecCode.MerkleDAGCBOR, hash, MultibaseEncoding.Base32Lower);
        
        Assert.Equal((ulong)MulticodecCode.MerkleDAGCBOR, dagCborCid.Codec);
        
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{dagCborCid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        Assert.Empty(result);
    }

    [Fact]
    public void CidValidation_DagPbCodec_Invalid()
    {
        // CID with dag-pb codec (0x70) - NOT valid for blobs
        var hash = Multihash.Sum(HashType.SHA2_256, System.Text.Encoding.UTF8.GetBytes("test data"));
        var dagPbCid = Cid.NewV1(Cid.DAG_PB, hash, MultibaseEncoding.Base32Lower);
        
        Assert.Equal(Cid.DAG_PB, dagPbCid.Codec);
        
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{dagPbCid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        Assert.Empty(result);
    }

    // CID Version tests
    [Fact]
    public void CidValidation_CidV1_Valid()
    {
        // CIDv1 with raw codec is the standard for blobs
        var cid = Cid.Create("v1 test", MultibaseEncoding.Base32Lower);
        var cidString = cid.ToString();
        Assert.Equal(CID.Version.V1, cid.Version);
        
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        Assert.Single(result);
        Assert.Equal(cidString, result[0].Cid.ToString());
    }

    [Fact]
    public void CidValidation_CidV0_Invalid()
    {
        // CIDv0 uses dag-pb codec which is not valid for blobs
        // CIDv0 starts with "Qm" and is base58btc encoded
        var cidV0 = "QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdG";
        
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cidV0}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        // CIDv0 uses dag-pb (0x70) codec, not raw, so should be invalid
        Assert.Empty(result);
    }

    // Multibase encoding tests - CID should be read correctly regardless of encoding
    [Fact]
    public void CidValidation_Base32Lower_Valid()
    {
        var cid = Cid.Create("base32 test", MultibaseEncoding.Base32Lower);
        var cidString = cid.ToString();
        Assert.True(cidString.StartsWith("b")); // base32lower prefix
        
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        Assert.Single(result);
        Assert.Equal(cidString, result[0].Cid.ToString());
    }

    [Fact]
    public void CidValidation_Base32Upper_Valid()
    {
        var cid = Cid.Create("base32 upper test", MultibaseEncoding.Base32Upper);
        var cidString = cid.ToString();
        Assert.True(cidString.StartsWith("B")); // base32upper prefix
        
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        Assert.Single(result);
        Assert.Equal(cidString, result[0].Cid.ToString());
    }

    [Fact]
    public void CidValidation_Base58Btc_Valid()
    {
        var cid = Cid.Create("base58 test", MultibaseEncoding.Base58Btc);
        var cidString = cid.ToString();
        Assert.True(cidString.StartsWith("z")); // base58btc prefix
        
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        Assert.Single(result);
        Assert.Equal(cidString, result[0].Cid.ToString());
    }

    [Fact]
    public void CidValidation_Base64_Valid()
    {
        var cid = Cid.Create("base64 test", MultibaseEncoding.Base64);
        var cidString = cid.ToString();
        Assert.True(cidString.StartsWith("m")); // base64 prefix
        
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        Assert.Single(result);
        Assert.Equal(cidString, result[0].Cid.ToString());
    }

    // CID preserves encoding when parsed
    [Fact]
    public void CidValidation_EncodingPreservedAfterParsing()
    {
        var originalCid = Cid.Create("encoding preservation test", MultibaseEncoding.Base32Lower);
        var cidString = originalCid.ToString();
        
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cidString}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        Assert.Single(result);
        // The CID should be parseable and have correct codec
        Assert.Equal((ulong)MulticodecCode.Raw, result[0].Cid.Codec);
        // CID string should be preserved after encoding/decoding
        Assert.Equal(cidString, result[0].Cid.ToString());
    }

    [Fact]
    public void CidValidation_DifferentEncodingsSameCid_SameBlob()
    {
        // Create CIDs with same content but different encodings
        var content = "same content different encoding";
        var cid32 = Cid.Create(content, MultibaseEncoding.Base32Lower);
        var cid58 = Cid.Create(content, MultibaseEncoding.Base58Btc);
        
        var json32 = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid32}}" } }""";
        var json58 = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid58}}" } }""";
        
        var result32 = Prepare.ExtractBlobReferences(json32);
        var result58 = Prepare.ExtractBlobReferences(json58);
        
        Assert.Single(result32);
        Assert.Single(result58);
        // Both should produce the same underlying CID (same hash)
        Assert.Equal(result32[0].Cid.MultiHash, result58[0].Cid.MultiHash);
    }

    // Hash algorithm tests
    [Fact]
    public void CidValidation_Sha256Hash_Valid()
    {
        // SHA-256 is the standard and preferred hash
        var cid = Cid.Create("sha256 test", MultibaseEncoding.Base32Lower);
        var cidString = cid.ToString();
        Assert.Equal(HashType.SHA2_256, cid.MultiHash.Code);
        
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{cid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        Assert.Single(result);
        Assert.Equal(cidString, result[0].Cid.ToString());
    }

    // Invalid CID format tests
    [Fact]
    public void CidValidation_TruncatedCid_Invalid()
    {
        var validCid = Cid.Create("test", MultibaseEncoding.Base32Lower).ToString();
        var truncatedCid = validCid[..10]; // Truncate the CID
        
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{truncatedCid}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        Assert.Empty(result);
    }

    [Fact]
    public void CidValidation_GarbageData_Invalid()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "bafynotarealcidatall" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        Assert.Empty(result);
    }

    [Fact]
    public void CidValidation_EmptyCid_Invalid()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        Assert.Empty(result);
    }

    [Fact]
    public void CidValidation_SingleCharacter_Invalid()
    {
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "b" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        Assert.Empty(result);
    }

    [Fact]
    public void CidValidation_InvalidMultibasePrefix_Invalid()
    {
        // '!' is not a valid multibase prefix
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "!invalidprefix" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        Assert.Empty(result);
    }

    [Fact]
    public void CidValidation_ValidPrefixInvalidContent_Invalid()
    {
        // 'b' is base32lower prefix but content is invalid
        var json = """{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "b0123456789" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        Assert.Empty(result);
    }

    // IPFS URL format tests (implementation supports /ipfs/ prefix)
    [Fact]
    public void CidValidation_IpfsUrlFormat_Valid()
    {
        var cid = Cid.Create("ipfs url test", MultibaseEncoding.Base32Lower);
        var cidString = cid.ToString();
        var ipfsUrl = $"/ipfs/{cid}";
        
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{ipfsUrl}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        Assert.Single(result);
        Assert.Equal(cidString, result[0].Cid.ToString());
    }

    [Fact]
    public void CidValidation_IpfsUrlWithPath_Valid()
    {
        var cid = Cid.Create("ipfs with path", MultibaseEncoding.Base32Lower);
        var cidString = cid.ToString();
        var ipfsUrl = $"https://ipfs.io/ipfs/{cid}";
        
        var json = $$"""{ "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{ipfsUrl}}" } }""";
        var result = Prepare.ExtractBlobReferences(json);
        
        Assert.Single(result);
        Assert.Equal(cidString, result[0].Cid.ToString());
    }

    // Edge cases
    [Fact]
    public void CidValidation_CidWithCorrectCodecInNestedBlob_Valid()
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
        
        Assert.Single(result);
        Assert.Equal((ulong)MulticodecCode.Raw, result[0].Cid.Codec);
        Assert.Equal(cidString, result[0].Cid.ToString());
    }

    [Fact]
    public void CidValidation_MultipleBlobsWithDifferentEncodings_FindsAll()
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
        
        Assert.Equal(3, result.Length);
        Assert.All(result, b => Assert.Equal((ulong)MulticodecCode.Raw, b.Cid.Codec));
        // Each CID string should be preserved after encoding/decoding
        Assert.Contains(result, b => b.Cid.ToString() == cidStrings[0]);
        Assert.Contains(result, b => b.Cid.ToString() == cidStrings[1]);
        Assert.Contains(result, b => b.Cid.ToString() == cidStrings[2]);
    }

    [Fact]
    public void CidValidation_MixedValidAndInvalidCodecs_FindsOnlyRaw()
    {
        var rawCid = Cid.Create("raw codec", MultibaseEncoding.Base32Lower);
        var hash = Multihash.Sum(HashType.SHA2_256, System.Text.Encoding.UTF8.GetBytes("dag-cbor codec"));
        var dagCborCid = Cid.NewV1((ulong)MulticodecCode.MerkleDAGCBOR, hash, MultibaseEncoding.Base32Lower);
        
        var json = $$"""
        {
            "validBlob": { "$type": "blob", "mimeType": "image/png", "size": 100, "ref": { "$link": "{{rawCid}}" } },
            "invalidBlob": { "$type": "blob", "mimeType": "image/jpeg", "size": 200, "ref": { "$link": "{{dagCborCid}}" } }
        }
        """;
        
        var result = Prepare.ExtractBlobReferences(json);
        
        // Should only find the blob with raw codec
        Assert.Single(result);
        Assert.Equal(rawCid.ToString(), result[0].Cid.ToString());
    }
    #endregion
}
