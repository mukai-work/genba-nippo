using GenbaNippo.Api.Application;
using GenbaNippo.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace GenbaNippo.Api.Controllers;

/// <summary>工程。</summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class WorkItemsController(WorkItemService service) : ControllerBase
{
    /// <summary>現場の工程一覧。今日時点の予定進捗率と遅れ判定つき。</summary>
    [HttpGet("sites/{siteId:int}/work-items")]
    [ProducesResponseType<IReadOnlyList<WorkItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<WorkItemResponse>> List(int siteId, CancellationToken ct) => service.ListAsync(siteId, ct);

    /// <summary>工程を登録する。予定期間は現場の工期の中に収める。</summary>
    [HttpPost("sites/{siteId:int}/work-items")]
    [ProducesResponseType<WorkItemResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkItemResponse>> Create(int siteId, WorkItemRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(siteId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>工程の詳細（今日時点の予定進捗率と遅れ判定つき）。</summary>
    [HttpGet("work-items/{id:int}")]
    [ProducesResponseType<WorkItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<WorkItemResponse> Get(int id, CancellationToken ct) => service.GetAsync(id, ct);

    /// <summary>工程の名前・予定期間を変更する（実績の進捗率は日報の承認でだけ変わる）。</summary>
    [HttpPut("work-items/{id:int}")]
    [ProducesResponseType<WorkItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<WorkItemResponse> Update(int id, WorkItemRequest request, CancellationToken ct) =>
        service.UpdateAsync(id, request, ct);
}
