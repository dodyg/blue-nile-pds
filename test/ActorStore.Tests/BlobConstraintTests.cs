using System.Text.Json;
using ActorStore.Repo;
using CID;
using Multiformats.Base;

namespace ActorStore.Tests;

public class BlobConstraintTests
{
    private static string GetValidCid() =>
        Cid.Create("test data", MultibaseEncoding.Base32Lower).ToString();

    [Test]
    public async Task ExtractBlobReferences_WithProfileAvatar_PopulatesConstraintAsync()
    {
        var cid = GetValidCid();
        var json = $$"""
            {
                "$type": "app.bsky.actor.profile",
                "displayName": "Test",
                "avatar": {
                    "$type": "blob",
                    "ref": { "$link": "{{cid}}" },
                    "mimeType": "image/png",
                    "size": 12345
                }
            }
            """;

        var doc = JsonDocument.Parse(json);
        var refs = Prepare.ExtractBlobReferences(doc.RootElement);

        await Assert.That(refs.Length).IsEqualTo(1);
        await Assert.That(refs[0].Constraints.Accept).IsNotNull();
        await Assert.That(refs[0].Constraints.Accept).Contains("image/png");
        await Assert.That(refs[0].Constraints.MaxSize).IsEqualTo(1_000_000);
    }

    [Test]
    public async Task ExtractBlobReferences_WithProfileBanner_PopulatesConstraintAsync()
    {
        var cid = GetValidCid();
        var json = $$"""
            {
                "$type": "app.bsky.actor.profile",
                "banner": {
                    "$type": "blob",
                    "ref": { "$link": "{{cid}}" },
                    "mimeType": "image/jpeg",
                    "size": 99999
                }
            }
            """;

        var doc = JsonDocument.Parse(json);
        var refs = Prepare.ExtractBlobReferences(doc.RootElement);

        await Assert.That(refs.Length).IsEqualTo(1);
        await Assert.That(refs[0].Constraints.Accept).IsNotNull();
        await Assert.That(refs[0].Constraints.MaxSize).IsEqualTo(1_000_000);
    }

    [Test]
    public async Task ExtractBlobReferences_UnknownField_NoConstraintAsync()
    {
        var cid = GetValidCid();
        var json = $$"""
            {
                "$type": "app.bsky.feed.post",
                "text": "hello",
                "embed": {
                    "$type": "app.bsky.embed.images",
                    "images": [{
                        "image": {
                            "$type": "blob",
                            "ref": { "$link": "{{cid}}" },
                            "mimeType": "image/png",
                            "size": 12345
                        }
                    }]
                }
            }
            """;

        var doc = JsonDocument.Parse(json);
        var refs = Prepare.ExtractBlobReferences(doc.RootElement);

        await Assert.That(refs.Length).IsEqualTo(1);
        // Unknown field -> no constraint
        await Assert.That(refs[0].Constraints.Accept).IsNull();
        await Assert.That(refs[0].Constraints.MaxSize).IsNull();
    }
}
