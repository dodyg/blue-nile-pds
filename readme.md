# atompds
atompds is a proof of concept ATProto PDS (Personal Data Server) instance implementation written in C#.
Most of the concepts have been manually translated from https://github.com/bluesky-social/atproto/tree/main/packages
and should follow the same general structure.

> [!NOTE]  
> I do not recommend using this for anything other than testing or experimenting.
> Updates will likely break compatibility with previous versions and there is no guarantee of data integrity.
> There may be some inaccuracies and misunderstandings in the implementation.
 
## TODO
- Blob storage, any writes that have a blob will explicitly throw currently
- Test cases for all components
- Proper lexicon validation
  - Note that I want to avoid implementing logic for each lexicon, a more generic approach based on the lexicon schema would be ideal. There is already lexicon specific logic in some components I would like to remove.
- Better proxying to appview, works but manually specifying each route isn't ideal.
- Rate-limit routes?
- Mailer service (for email verification)
- XRPC error is used in a bunch of places it shouldn't for the sake of simplicity
  - would be nicer for an errordetail exception type which can be caught and wrapped at the api level much like what is done for the xrpc error type already.
- PutRecord currently fails to check if records exist (due to cid mismatches agains the repo_block table) and thus fails to update the mst due to collisions since it will always use a create operation.
- Better appview fallback support for other routes
  - Currently, it's a manual process to pipe certain routes to an appview (i.e. resolvehandle) it would be nicer to have a more general approach, perhaps using FishyFlip. 
- Repo structure cleanup
  - The flat structure here really isn't doing me any favors, I would like to move everything to a ./src directory and move tests to a ./tests directory. Plus reorganize the project directories within that a bit.
- Docker and docker compose setup
  - Fortunately the project runs as a single instance so this shouldn't be too difficult.

## What has been done so far
- Account and session creation
- Account deletion
  - Lacking email verification, currently the 2-factor code is just printed to the console
- Record creation and deletion
- JWT validation
- AppView proxying
- Firehose (subscriberepos)
  - Caveat here is that if any event we produce fails validation on the crawlers side, the wss connection is cancelled by the crawler and can prevent further events from being crawled. 
- Automated crawl requests
  - Crawlers are notified when new records are sequenced.
- Identity resolution
  - As long as your handle domain is the same as the server's handles are resolved via the [HTTP method](https://bsky.social/about/blog/4-28-2023-domain-handle-tutorial)

## Components
- Crypto (implements @atproto/crypto)
  - Cryptographic helper functions
  - K-256 (secp256k1) support
- Repo (implements @atproto/repo)
  - Merkle Search Tree (MST) structure
  - Content Addressable Archive (CAR) encoding
- CID
  - Content Identifier (CID) generation and parsing
- DidLib
  - Minimal implementation to upload DID documents to a PLC Directory
- Identity (implements @atproto/identity)
  - did:web: and did:plc: resolution
  - _atproto dns resolution
- Common
  - TID generation
  - CBOR block base handling

## Configuration
Copy `atompds/appsettings.Development.json.example` to `atompds/appsettings.Development.json` and edit it.
There are more options found in `atompds/Config/ServerEnvironment.cs` that can also be specified in config.

