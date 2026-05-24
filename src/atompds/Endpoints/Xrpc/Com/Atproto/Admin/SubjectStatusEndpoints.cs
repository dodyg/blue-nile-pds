using AccountManager;
using AccountManager.Db;
using ActorStore;
using CarpaNet;
using ComAtproto.Repo;
using atompds.Middleware;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Repo;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Admin;

public static class SubjectStatusEndpoints
{
    private const string DefaultTakedownRef = "admin-takedown";

    public static RouteGroupBuilder MapSubjectStatusEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.admin.getSubjectStatus", GetAsync).WithMetadata(new AdminTokenAttribute());
        group.MapPost("com.atproto.admin.updateSubjectStatus", UpdateAsync).WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> GetAsync(
        HttpContext context,
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        AccountManagerDb accountManagerDb,
        ILogger<Program> logger,
        string? did,
        string? uri,
        string? blob)
    {
        EnsureSingleSubjectSelector(did, uri, blob);

        if (!string.IsNullOrWhiteSpace(did))
        {
            var account = await GetRequiredAccountAsync(did, accountRepository);
            return Results.Ok(new GetSubjectStatusOutput
            {
                Subject = new RepoRefSubject { Did = account.Did },
                Takedown = ToStatusAttr(account.TakedownRef),
                Deactivated = ToAppliedStatusAttr(account.DeactivatedAt != null)
            });
        }

        if (!string.IsNullOrWhiteSpace(uri))
        {
            var recordStatus = await GetRecordStatusAsync(ParseAtUri(uri), accountRepository, actorRepositoryProvider);
            return Results.Ok(new GetSubjectStatusOutput
            {
                Subject = new StrongRefSubject { Uri = recordStatus.Uri, Cid = recordStatus.Cid },
                Takedown = ToStatusAttr(recordStatus.TakedownRef)
            });
        }

        var blobStatus = await GetBlobStatusAsync(blob!, accountRepository, actorRepositoryProvider, accountManagerDb, logger);
        return Results.Ok(new GetSubjectStatusOutput
        {
            Subject = new RepoBlobRefSubject { Did = blobStatus.Did, Cid = blobStatus.Cid, RecordUri = blobStatus.RecordUri },
            Takedown = ToStatusAttr(blobStatus.TakedownRef)
        });
    }

    private static async Task<IResult> UpdateAsync(
        UpdateSubjectStatusInput request,
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        AccountManagerDb accountManagerDb,
        ILogger<Program> logger)
    {
        var subject = NormalizeSubject(request);

        switch (subject)
        {
            case RepoSubjectInput repoSubject:
            {
                await GetRequiredAccountAsync(repoSubject.Did, accountRepository);

                if (request.Takedown != null)
                    await accountRepository.UpdateTakedownRefAsync(repoSubject.Did, request.Takedown.Applied ? request.Takedown.Ref ?? DefaultTakedownRef : null);

                if (request.Deactivated != null)
                {
                    if (request.Deactivated.Applied)
                        await accountRepository.DeactivateAccountAsync(repoSubject.Did, null);
                    else
                        await accountRepository.ActivateAccountAsync(repoSubject.Did);
                }

                var account = await GetRequiredAccountAsync(repoSubject.Did, accountRepository);
                return Results.Ok(new UpdateSubjectStatusOutput
                {
                    Subject = new RepoRefSubject { Did = account.Did },
                    Takedown = ToStatusAttr(account.TakedownRef)
                });
            }
            case RecordSubjectInput recordSubject:
            {
                if (request.Deactivated != null)
                    throw InvalidRequest("deactivated can only be set for repo subjects");

                var updatedRecord = await UpdateRecordStatusAsync(recordSubject, request.Takedown, accountRepository, actorRepositoryProvider);
                return Results.Ok(new UpdateSubjectStatusOutput
                {
                    Subject = new StrongRefSubject { Uri = updatedRecord.Uri, Cid = updatedRecord.Cid },
                    Takedown = ToStatusAttr(updatedRecord.TakedownRef)
                });
            }
            case BlobSubjectInput blobSubject:
            {
                if (request.Deactivated != null)
                    throw InvalidRequest("deactivated can only be set for repo subjects");

                var updatedBlob = await UpdateBlobStatusAsync(blobSubject, request.Takedown, accountRepository, actorRepositoryProvider);
                return Results.Ok(new UpdateSubjectStatusOutput
                {
                    Subject = new RepoBlobRefSubject { Did = updatedBlob.Did, Cid = updatedBlob.Cid, RecordUri = updatedBlob.RecordUri },
                    Takedown = ToStatusAttr(updatedBlob.TakedownRef)
                });
            }
            default:
                throw InvalidRequest("unsupported subject");
        }
    }

    private static StatusAttr ToStatusAttr(string? refValue) =>
        new StatusAttr { Applied = refValue != null, Ref = refValue };

    private static StatusAttr ToAppliedStatusAttr(bool applied) =>
        new StatusAttr { Applied = applied };

    private static async Task<ActorAccount> GetRequiredAccountAsync(string did, AccountRepository accountRepository)
    {
        var account = await accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null) throw InvalidRequest("Account not found");
        return account;
    }

    private static async Task<RecordSubjectState> GetRecordStatusAsync(ATUri subjectUri, AccountRepository accountRepository, ActorRepositoryProvider actorRepositoryProvider)
    {
        var did = subjectUri.Authority;
        if (string.IsNullOrWhiteSpace(did)) throw InvalidRequest("uri must include a DID authority");

        await GetRequiredAccountAsync(did, accountRepository);

        await using var actorRepo = actorRepositoryProvider.Open(did);
        var record = await actorRepo.Record.GetRecordAsync(subjectUri, null, includeSoftDeleted: true);
        if (record == null) throw InvalidRequest("Record not found");

        return new RecordSubjectState(record.Uri, record.Cid, record.TakedownRef);
    }

    private static async Task<RecordSubjectState> UpdateRecordStatusAsync(RecordSubjectInput subject, StatusAttr? takedown, AccountRepository accountRepository, ActorRepositoryProvider actorRepositoryProvider)
    {
        var did = subject.Uri.Authority;
        if (string.IsNullOrWhiteSpace(did)) throw InvalidRequest("uri must include a DID authority");

        await GetRequiredAccountAsync(did, accountRepository);

        await using var actorRepo = actorRepositoryProvider.Open(did);
        var updated = await actorRepo.TransactDbAsync(async db =>
        {
            var record = await db.Records.FirstOrDefaultAsync(x => x.Uri == subject.Uri.ToString() && x.Cid == subject.Cid);
            if (record == null) return null;

            if (takedown != null)
            {
                record.TakedownRef = takedown.Applied ? takedown.Ref ?? DefaultTakedownRef : null;
                await db.SaveChangesAsync();
            }

            return new RecordSubjectState(record.Uri, record.Cid, record.TakedownRef);
        });

        return updated ?? throw InvalidRequest("Record not found");
    }

    private static async Task<BlobSubjectState> GetBlobStatusAsync(string cid, AccountRepository accountRepository, ActorRepositoryProvider actorRepositoryProvider, AccountManagerDb accountManagerDb, ILogger logger)
    {
        BlobSubjectState? match = null;

        var dids = await accountManagerDb.Actors.AsNoTracking().Select(x => x.Did).ToListAsync();

        foreach (var did in dids)
        {
            if (!actorRepositoryProvider.Exists(did)) continue;

            await using var actorRepo = actorRepositoryProvider.Open(did);
            var candidate = await actorRepo.TransactDbAsync(async db =>
            {
                var blob = await db.Blobs.AsNoTracking().FirstOrDefaultAsync(x => x.Cid == cid);
                if (blob == null) return null;

                var recordUri = await db.RecordBlobs.AsNoTracking()
                    .Where(x => x.BlobCid == cid)
                    .Select(x => x.RecordUri)
                    .FirstOrDefaultAsync();

                return new BlobSubjectState(did, blob.Cid, recordUri, blob.TakedownRef);
            });

            if (candidate == null) continue;

            if (match != null)
            {
                logger.LogWarning("Blob {Cid} matched multiple repos while resolving subject status", cid);
                throw InvalidRequest("blob matched multiple repos");
            }

            match = candidate;
        }

        return match ?? throw InvalidRequest("Blob not found");
    }

    private static async Task<BlobSubjectState> UpdateBlobStatusAsync(BlobSubjectInput subject, StatusAttr? takedown, AccountRepository accountRepository, ActorRepositoryProvider actorRepositoryProvider)
    {
        await GetRequiredAccountAsync(subject.Did, accountRepository);
        await using var actorRepo = actorRepositoryProvider.Open(subject.Did);

        var updated = await actorRepo.TransactDbAsync(async db =>
        {
            var blob = await db.Blobs.FirstOrDefaultAsync(x => x.Cid == subject.Cid);
            if (blob == null) return null;

            if (!string.IsNullOrWhiteSpace(subject.RecordUri))
            {
                var hasRecordUri = await db.RecordBlobs.AsNoTracking()
                    .AnyAsync(x => x.BlobCid == subject.Cid && x.RecordUri == subject.RecordUri);
                if (!hasRecordUri) throw InvalidRequest("Blob not found for recordUri");
            }

            if (takedown != null)
            {
                blob.TakedownRef = takedown.Applied ? takedown.Ref ?? DefaultTakedownRef : null;
                await db.SaveChangesAsync();
            }

            var recordUri = subject.RecordUri ?? await db.RecordBlobs.AsNoTracking()
                .Where(x => x.BlobCid == subject.Cid)
                .Select(x => x.RecordUri)
                .FirstOrDefaultAsync();

            return new BlobSubjectState(subject.Did, blob.Cid, recordUri, blob.TakedownRef);
        });

        return updated ?? throw InvalidRequest("Blob not found");
    }

    private static void EnsureSingleSubjectSelector(string? did, string? uri, string? blob)
    {
        var supplied = 0;
        if (!string.IsNullOrWhiteSpace(did)) supplied++;
        if (!string.IsNullOrWhiteSpace(uri)) supplied++;
        if (!string.IsNullOrWhiteSpace(blob)) supplied++;
        if (supplied != 1) throw InvalidRequest("exactly one of did, uri, or blob is required");
    }

    private static SubjectInput NormalizeSubject(UpdateSubjectStatusInput request)
    {
        if (request.Subject != null && !string.IsNullOrWhiteSpace(request.Did))
            throw InvalidRequest("provide either subject or did, not both");

        if (request.Subject == null)
        {
            if (string.IsNullOrWhiteSpace(request.Did)) throw InvalidRequest("subject is required");
            return new RepoSubjectInput(request.Did);
        }

        var hasDid = !string.IsNullOrWhiteSpace(request.Subject.Did);
        var hasUri = !string.IsNullOrWhiteSpace(request.Subject.Uri);
        var hasCid = !string.IsNullOrWhiteSpace(request.Subject.Cid);
        var hasRecordUri = !string.IsNullOrWhiteSpace(request.Subject.RecordUri);

        if (hasDid && !hasUri && !hasCid && !hasRecordUri) return new RepoSubjectInput(request.Subject.Did!);
        if (!hasDid && hasUri && hasCid && !hasRecordUri) return new RecordSubjectInput(ParseAtUri(request.Subject.Uri!), request.Subject.Cid!);
        if (hasDid && !hasUri && hasCid) return new BlobSubjectInput(request.Subject.Did!, request.Subject.Cid!, request.Subject.RecordUri);

        throw InvalidRequest("subject must be a repoRef, strongRef, or repoBlobRef");
    }

    private static ATUri ParseAtUri(string uri)
    {
        try { return new ATUri(uri); }
        catch (Exception) { throw InvalidRequest("invalid at-uri"); }
    }

    private static XRPCError InvalidRequest(string message) => new(new InvalidRequestErrorDetail(message));

    private abstract record SubjectInput;
    private sealed record RepoSubjectInput(string Did) : SubjectInput;
    private sealed record RecordSubjectInput(ATUri Uri, string Cid) : SubjectInput;
    private sealed record BlobSubjectInput(string Did, string Cid, string? RecordUri) : SubjectInput;
    private sealed record RecordSubjectState(string Uri, string Cid, string? TakedownRef);
    private sealed record BlobSubjectState(string Did, string Cid, string? RecordUri, string? TakedownRef);
}

public sealed class UpdateSubjectStatusInput
{
    public string? Did { get; set; }
    public SubjectRef? Subject { get; set; }
    public StatusAttr? Takedown { get; set; }
    public StatusAttr? Deactivated { get; set; }
}

public sealed class SubjectRef
{
    public string? Did { get; set; }
    public string? Uri { get; set; }
    public string? Cid { get; set; }
    public string? RecordUri { get; set; }
}

public sealed class StatusAttr
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public bool Applied { get; set; }
    public string? Ref { get; set; }
}

public sealed class RepoRefSubject
{
    public required string Did { get; init; }
}

public sealed class StrongRefSubject
{
    public required string Uri { get; init; }
    public required string Cid { get; init; }
}

public sealed class RepoBlobRefSubject
{
    public required string Did { get; init; }
    public required string Cid { get; init; }
    public string? RecordUri { get; init; }
}

public sealed class GetSubjectStatusOutput
{
    public required object Subject { get; init; }
    public StatusAttr? Takedown { get; init; }
    public StatusAttr? Deactivated { get; init; }
}

public sealed class UpdateSubjectStatusOutput
{
    public required object Subject { get; init; }
    public StatusAttr? Takedown { get; init; }
}
