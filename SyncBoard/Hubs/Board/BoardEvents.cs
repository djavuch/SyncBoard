namespace SyncBoard.Hubs.Board;

public record CardCreatedEvent(Guid CardId, string Title, Guid? ColumnId, int Position);
public record CardUpdatedEvent(Guid CardId, string Title);
public record CardMovedEvent(Guid CardId, Guid FromColumnId, Guid ToColumnId, int Position);
public record CardDeletedEvent(Guid CardId);
public record ColumnCreatedEvent(Guid ColumnId, string Title, int Position);
public record ColumnUpdatedEvent(Guid ColumnId, string Title, int Position);
public record ColumnMovedEvent(Guid ColumnId, int FromPosition, int ToPosition);
public record ColumnDeletedEvent(Guid ColumnId);