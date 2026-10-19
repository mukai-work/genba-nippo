namespace GenbaNippo.Domain;

/// <summary>業務ルールに反する入力（例: 工期の終了日が開始日より前）。API では 400 になる。</summary>
public class DomainRuleException(string message) : Exception(message);

/// <summary>今の状態では許されない操作（例: 承認済みの日報を編集）。API では 409 になる。</summary>
public class InvalidStateException(string message) : Exception(message);
