namespace AccountManager.Db;

public record AvailabilityFlags(bool IncludeTakenDown = false, bool IncludeDeactivated = false);