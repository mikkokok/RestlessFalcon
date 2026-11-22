using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RestlessFalcon.State;

public sealed record CreateStateRequest(string Key, JsonElement Value);
public sealed record UpdateStateRequest(JsonElement Value);
public sealed record StateEntry(string Namespace, string Key, JsonElement Value,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record StateKey(string Key, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public interface IStateStore
{
    Task<IReadOnlyList<StateKey>> ListAsync(string ns, int skip, int take, CancellationToken cancellationToken);
    Task<StateEntry?> GetAsync(string ns, string key, CancellationToken cancellationToken);
    Task<StateEntry?> CreateAsync(string ns, string key, JsonElement value, CancellationToken cancellationToken);
    Task<StateEntry?> UpdateAsync(string ns, string key, JsonElement value, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(string ns, string key, CancellationToken cancellationToken);
}
