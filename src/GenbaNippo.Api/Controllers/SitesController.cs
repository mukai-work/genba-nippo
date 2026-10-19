using GenbaNippo.Api.Application;
using GenbaNippo.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace GenbaNippo.Api.Controllers;

/// <summary>現場。</summary>
[ApiController]
[Route("api/sites")]
[Produces("application/json")]
public sealed class SitesController(SiteService service) : ControllerBase
{
    /// <summary>現場の一覧（承認待ちの日報数・遅れている工程数つき）。</summary>
    [HttpGet]
    public Task<IReadOnlyList<SiteResponse>> List(CancellationToken ct) => service.ListAsync(ct);

    /// <summary>現場の詳細。</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<SiteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<SiteResponse> Get(int id, CancellationToken ct) => service.GetAsync(id, ct);

    /// <summary>現場を登録する。</summary>
    [HttpPost]
    [ProducesResponseType<SiteResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SiteResponse>> Create(CreateSiteRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }
}
