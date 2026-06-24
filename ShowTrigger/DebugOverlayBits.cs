using System;

namespace ShowTrigger;

/// <summary>
/// Source 2 entity debug-overlay flags (engine <c>DebugOverlayBits_t</c>). An entity renders the
/// corresponding debug visual when the matching bit is set in its overlay set and the overlays are
/// broadcast (<c>sv_debug_overlays_broadcast 1</c>) to a client that opted in
/// (<c>cl_debug_overlays_broadcast 1</c>).
///
/// Full enum transcribed from the engine definition (the <c>MPropertyFriendlyName</c> /
/// <c>MPropertyDescription</c> metadata is preserved as XML docs). Values are stable engine
/// constants. The one this plugin uses for trigger zones is <see cref="TriggerBounds"/> (0x2000) —
/// the exact bit CS2's <c>showtriggers</c> command sets.
/// </summary>
[Flags]
public enum DebugOverlayBits : ulong
{
    None = 0,

    /// <summary>"Ent Text" — show text debug overlay for this entity.</summary>
    Text = 0x1,
    /// <summary>"Name" — show name debug overlay for this entity.</summary>
    Name = 0x2,
    /// <summary>"Bounding Box" — show bounding box overlay for this entity.</summary>
    BoundingBox = 0x4,
    /// <summary>"Pivot" — show pivot for this entity.</summary>
    Pivot = 0x8,
    /// <summary>"Message" — show messages for this entity.</summary>
    Message = 0x10,
    /// <summary>"ABS BBox" — show abs bounding box overlay.</summary>
    AbsBox = 0x20,
    /// <summary>"RBox" — show the rbox overlay.</summary>
    RBox = 0x40,
    /// <summary>"Entities That Block LOS" — show entities that block NPC LOS.</summary>
    ShowBlocksLos = 0x80,
    /// <summary>"Attachment Points" — show attachment points.</summary>
    Attachments = 0x100,
    /// <summary>"Interpolated Attachment Points".</summary>
    InterpolatedAttachments = 0x200,
    /// <summary>"Interpolated Pivot".</summary>
    InterpolatedPivot = 0x400,
    /// <summary>"Skeleton" — show skeleton for this entity.</summary>
    Skeleton = 0x800,
    /// <summary>"Interpolated Skeleton".</summary>
    InterpolatedSkeleton = 0x1000,
    /// <summary>"Trigger Bounds" — show trigger bounds. Used by the engine's <c>showtriggers</c>.</summary>
    TriggerBounds = 0x2000,
    /// <summary>"Hitboxes" — show hitboxes for this entity.</summary>
    Hitbox = 0x4000,
    /// <summary>"Interpolated Hitboxes".</summary>
    InterpolatedHitbox = 0x8000,
    /// <summary>"Autoaim Radius" — display autoaim radius.</summary>
    Autoaim = 0x10000,
    /// <summary>"NPC Selected".</summary>
    NpcSelected = 0x20000,
    /// <summary>"Joint Info" — show joint info for this entity.</summary>
    JointInfo = 0x40000,
    /// <summary>"NPC Route" — draw the route for this npc.</summary>
    NpcRoute = 0x80000,
    /// <summary>Visibility traces.</summary>
    VisibilityTraces = 0x100000,
    // 0x200000 is unused in the engine enum.
    /// <summary>"NPC Enemies" — show npc's enemies.</summary>
    NpcEnemies = 0x400000,
    /// <summary>"NPC Conditions".</summary>
    NpcConditions = 0x800000,
    /// <summary>"NPC Combat" — squads/slots/etc.</summary>
    NpcCombat = 0x1000000,
    /// <summary>"NPC Schedule Tasks".</summary>
    NpcTask = 0x2000000,
    /// <summary>"NPC Body Locations".</summary>
    NpcBodyLocations = 0x4000000,
    /// <summary>"NPC View Cone".</summary>
    NpcViewcone = 0x8000000,
    /// <summary>"NPC Kill" — kill the NPC, running all appropriate AI.</summary>
    NpcKill = 0x10000000,
    /// <summary>"OVERLAY_WC_CHANGE_ENTITY" — object changed during WC edit.</summary>
    WcChangeEntity = 0x20000000,
    /// <summary>"Buddha Mode" — take damage but don't die.</summary>
    BuddhaMode = 0x40000000,
    /// <summary>"NPC Steering" — steering regulations associated with the NPC.</summary>
    NpcSteeringRegulations = 0x80000000,
    /// <summary>"NPC Task Console Text".</summary>
    NpcTaskText = 0x100000000,
    /// <summary>"Prop Debug" — show prop health and bounds.</summary>
    PropDebug = 0x200000000,
    /// <summary>"NPC Relationships".</summary>
    NpcRelation = 0x400000000,
    /// <summary>"View Offset".</summary>
    ViewOffset = 0x800000000,
    /// <summary>"Collision Wireframe".</summary>
    VCollideWireframe = 0x1000000000,
    /// <summary>"NPC Scripted Commands".</summary>
    NpcScriptedCommands = 0x2000000000,
    /// <summary>"Actor Name".</summary>
    ActorName = 0x4000000000,
    /// <summary>"NPC Gather Conditions".</summary>
    NpcConditionsText = 0x8000000000,
    /// <summary>"NPC Ability Ranges".</summary>
    NpcAbilityRangeDebug = 0x10000000000,
}
