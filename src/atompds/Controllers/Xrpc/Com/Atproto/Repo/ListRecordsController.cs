using System.Text.Json;
using AccountManager;
using ActorStore;
using atompds.Utils;
using FishyFlip.Lexicon.Com.Atproto.Repo;
using FishyFlip.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Repo;


[Route("xrpc")]
[ApiController]
public class ListRecordsController(
    ActorRepositoryProvider actorRepositoryProvider,
    AccountRepository accountRepository
) : ControllerBase
{
    [HttpGet("com.atproto.repo.listRecords")]
    public async Task<IActionResult> ListRecords(
        [FromQuery] string repo,
        [FromQuery] string collection,
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        [FromQuery] bool reverse = false
    )
    {
        var did = await accountRepository.GetDidForActor(repo);

        if (did is null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail($"Could not find repo: {repo}"));
        }


        await using var actorRepo = actorRepositoryProvider.Open(did);

        var records = await actorRepo.Repo.Record.ListRecordsForCollection(
            collection,
            limit,
            reverse,
            cursor
        );

        var last = records.LastOrDefault();
        ATUri? lastUri = last is not null ? new ATUri(last.Uri!) : null;

    

        return Ok(new
        {
            cursor = lastUri?.Rkey,
            records = records.Select(r => new
            {
                uri = r.Uri,
                cid = r.Cid,
                value = r.Value.ToObjectResult()
            }).ToArray()
        });
    }

}

