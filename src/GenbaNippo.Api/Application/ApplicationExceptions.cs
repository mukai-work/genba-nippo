namespace GenbaNippo.Api.Application;

/// <summary>指定された ID のデータがない。404 になる。</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>画面が読み込んだ後に、他の人がデータを更新していた。409 になる。</summary>
public sealed class ConcurrencyConflictException()
    : Exception("他の人が先にこのデータ（日報または工程）を更新しました。画面を再読み込みしてから、もう一度操作してください。");

/// <summary>一意であるべきデータが既にある（例: 同じ日の日報）。409 になる。</summary>
public sealed class DuplicateException(string message) : Exception(message);
