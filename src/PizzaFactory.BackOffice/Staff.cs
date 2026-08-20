namespace PizzaFactory.BackOffice;

public enum StaffRole
{
    Pizzaiolo,
    Service,
    Courier,
}

public enum ShiftSlot
{
    Lunch,
    Dinner,
}

/// <summary>A member of the brigade. Certifications gate who may cover which shift.</summary>
public sealed record StaffMember(
    string Name,
    StaffRole Role,
    bool OvenCertified,
    string Quirk);

/// <summary>One staffing need on the rota: a slot on a date, a role, and who holds it (null = open).</summary>
public sealed record RotaEntry(
    DateOnly Date,
    ShiftSlot Slot,
    StaffRole Role,
    string? AssignedTo);

/// <summary>An absence: who is out, when. TrattoriaSoft never records why — Nonna doesn't gossip.</summary>
public sealed record Absence(string Name, DateOnly Date, ShiftSlot? Slot);

/// <summary>The house roster — nine people, fixed and characterful, seeded once.</summary>
public static class Roster
{
    public static IReadOnlyList<StaffMember> Members { get; } =
    [
        new("Sofia", StaffRole.Pizzaiolo, OvenCertified: true, "talks to the dough; the dough listens"),
        new("Luca", StaffRole.Pizzaiolo, OvenCertified: true, "times the 90 seconds by heartbeat"),
        new("Giulia", StaffRole.Pizzaiolo, OvenCertified: false, "apprentice — great hands, not yet oven-certified"),
        new("Maria", StaffRole.Service, OvenCertified: false, "remembers every regular's usual"),
        new("Elena", StaffRole.Service, OvenCertified: false, "carries four plates and a grudge"),
        new("Rosa", StaffRole.Service, OvenCertified: false, "smiles in twelve languages"),
        new("Paolo", StaffRole.Service, OvenCertified: false, "service by trade, courier by adrenaline"),
        new("Marco", StaffRole.Courier, OvenCertified: false, "knows every shortcut and one police officer by name"),
        new("Antonio", StaffRole.Courier, OvenCertified: false, "the Vespa whisperer"),
    ];

    public static StaffMember? Find(string name) =>
        Members.FirstOrDefault(m => string.Equals(m.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));
}
