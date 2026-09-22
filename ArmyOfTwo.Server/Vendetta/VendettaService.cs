using ArmyOfTwo.Server.Configuration;
using ArmyOfTwo.Server.Spawning;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Services.Modding;

namespace ArmyOfTwo.Server.Vendetta;

[Injectable(InjectionType = InjectionType.Singleton)]
public sealed class VendettaService(
    ProfileDataService profileDataService,
    SaveServer saveServer,
    ConfigService configService,
    SpawnService spawnService,
    MailSendService mailSendService,
    ISptLogger<VendettaService> logger)
{
    private const string ProfileDataKey = "spt.senze.armyoftwo.vendetta";
    private const string RookMask = "6aa6751e9c58c1980247451a";
    private const string TombstoneMask = "6aa673267c3f9661de93cdd3";
    private const string RookSenderId = "726f6f6b0000000000000000";
    private const string ThreatMessage =
        "Boundaries have been crossed. Now the consequences will follow. Prepare!";

    private readonly SemaphoreSlim syncRoot = new(1, 1);
    private readonly Dictionary<string, VendettaRaidStatus> currentStatuses =
        new(StringComparer.Ordinal);

    public async Task<VendettaRaidStatus> PrepareRaidAsync(
        MongoId sessionId,
        StartLocalRaidRequestData request,
        CancellationToken cancellationToken)
    {
        await syncRoot.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await LoadStateAsync(sessionId, cancellationToken).ConfigureAwait(false);
            var raidId = string.IsNullOrWhiteSpace(request.ServerId)
                ? Guid.NewGuid().ToString("N")
                : request.ServerId;

            if (!string.Equals(state.ActiveRaidId, raidId, StringComparison.Ordinal))
            {
                ResetRaidState(state);
                state.ActiveRaidId = raidId;
                state.PreRaidMaskItemIds = GetPreRaidMaskIds(sessionId);
                state.HuntActive = state.Marked &&
                    spawnService.IsMapEnabled(configService.Load().Spawns, request.Location);
            }

            RepairRookDialogue(sessionId, state.Marked);
            EnsureThreatMessage(sessionId, state);
            await SaveStateAsync(sessionId, state, cancellationToken).ConfigureAwait(false);

            var status = CreateStatus(state, request.Location);
            currentStatuses[sessionId.ToString()] = status;
            if (status.HuntActive)
            {
                logger.Warning(
                    $"Army of Two: vendetta hunt active on '{request.Location}'; spawn forced to 100%.");
            }

            return status;
        }
        finally
        {
            syncRoot.Release();
        }
    }

    public async Task<VendettaRaidStatus> RecordEventAsync(
        MongoId sessionId,
        VendettaEventRequest request,
        CancellationToken cancellationToken)
    {
        await syncRoot.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await LoadStateAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(state.ActiveRaidId))
            {
                return CreateStatus(state, string.Empty);
            }

            var isRook = string.Equals(request.Boss, "rook", StringComparison.OrdinalIgnoreCase);
            var isTombstone = string.Equals(request.Boss, "tombstone", StringComparison.OrdinalIgnoreCase);
            if (!isRook && !isTombstone)
            {
                return CreateStatus(state, string.Empty);
            }

            var eventLevel = ParseEventLevel(request.Event);
            var changed = false;

            if (state.HuntActive && eventLevel == VendettaEventLevel.Kill)
            {
                if (isRook && !state.RookKilledDuringHunt)
                {
                    state.RookKilledDuringHunt = true;
                    changed = true;
                }
                else if (isTombstone && !state.TombstoneKilledDuringHunt)
                {
                    state.TombstoneKilledDuringHunt = true;
                    changed = true;
                }
            }

            if (!state.Marked && eventLevel != VendettaEventLevel.None &&
                (eventLevel != VendettaEventLevel.Kill || request.KilledByPlayer))
            {
                var currentLevel = isRook ? state.RookEventLevel : state.TombstoneEventLevel;
                if (eventLevel > currentLevel)
                {
                    var previousHeat = state.Heat;
                    state.Heat = Math.Min(
                        GetHeatLimit(),
                        state.Heat + GetHeat(eventLevel) - GetHeat(currentLevel));
                    if (isRook)
                    {
                        state.RookEventLevel = eventLevel;
                    }
                    else
                    {
                        state.TombstoneEventLevel = eventLevel;
                    }

                    changed = true;
                    logger.Info(
                        $"Army of Two: {request.Boss} escalated to {eventLevel}; " +
                        $"Heat {previousHeat} -> {state.Heat}.");
                }
            }

            if (changed)
            {
                await SaveStateAsync(sessionId, state, cancellationToken).ConfigureAwait(false);
            }

            var status = CreateStatus(state, GetCurrentLocation(sessionId));
            currentStatuses[sessionId.ToString()] = status;
            return status;
        }
        finally
        {
            syncRoot.Release();
        }
    }

    public async Task<VendettaRaidStatus> ProcessRaidEndAsync(
        MongoId sessionId,
        EndLocalRaidRequestData request,
        CancellationToken cancellationToken)
    {
        await syncRoot.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await LoadStateAsync(sessionId, cancellationToken).ConfigureAwait(false);
            var raidId = string.IsNullOrWhiteSpace(request.ServerId)
                ? state.ActiveRaidId
                : request.ServerId;

            if (!string.IsNullOrWhiteSpace(raidId) &&
                string.Equals(state.LastProcessedRaidId, raidId, StringComparison.Ordinal))
            {
                return CreateStatus(state, string.Empty);
            }

            var survived = request.Results?.Result == ExitStatus.SURVIVED;
            if (survived && !state.Marked)
            {
                var items = request.Results?.Profile?.Inventory?.Items ?? [];
                AddExtractedMaskHeat(state, items, RookMask, "Rook");
                AddExtractedMaskHeat(state, items, TombstoneMask, "Tombstone");
            }

            var huntResolved = state.HuntActive && survived &&
                state.RookKilledDuringHunt && state.TombstoneKilledDuringHunt;

            if (huntResolved)
            {
                state.Heat = 0;
                state.Marked = false;
                state.ThreatMessageSent = false;
                logger.Info("Army of Two: vendetta hunt resolved; Heat reset to 0.");
            }
            else
            {
                MarkIfThresholdReached(state);
                EnsureThreatMessage(sessionId, state);
            }

            state.LastProcessedRaidId = raidId ?? string.Empty;
            ResetRaidState(state);
            await SaveStateAsync(sessionId, state, cancellationToken).ConfigureAwait(false);

            var status = CreateStatus(state, string.Empty);
            currentStatuses[sessionId.ToString()] = status;
            return status;
        }
        finally
        {
            syncRoot.Release();
        }
    }

    public async Task<VendettaRaidStatus> GetStatusAsync(
        MongoId sessionId,
        CancellationToken cancellationToken)
    {
        await syncRoot.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (currentStatuses.TryGetValue(sessionId.ToString(), out var current))
            {
                return current;
            }

            var state = await LoadStateAsync(sessionId, cancellationToken).ConfigureAwait(false);
            var status = CreateStatus(state, string.Empty);
            currentStatuses[sessionId.ToString()] = status;
            return status;
        }
        finally
        {
            syncRoot.Release();
        }
    }

    private async Task<VendettaProfileState> LoadStateAsync(
        MongoId sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!profileDataService.ProfileDataExists(sessionId.ToString(), ProfileDataKey))
            {
                return new VendettaProfileState();
            }

            var state = await profileDataService
                .GetProfileDataAsync<VendettaProfileState>(sessionId, ProfileDataKey, cancellationToken)
                .ConfigureAwait(false) ?? new VendettaProfileState();
            Normalize(state);
            return state;
        }
        catch (Exception exception)
        {
            logger.Error(
                $"Army of Two: could not load vendetta state for profile '{sessionId}': {exception.Message}");
            return new VendettaProfileState();
        }
    }

    private Task SaveStateAsync(
        MongoId sessionId,
        VendettaProfileState state,
        CancellationToken cancellationToken)
    {
        Normalize(state);
        return profileDataService.SaveProfileDataAsync(
            sessionId,
            ProfileDataKey,
            state,
            cancellationToken);
    }

    private HashSet<string> GetPreRaidMaskIds(MongoId sessionId)
    {
        try
        {
            var items = saveServer.GetProfile(sessionId)?.CharacterData?.PmcData?.Inventory?.Items ?? [];
            return items
                .Where(item => IsMask(item, RookMask) || IsMask(item, TombstoneMask))
                .Select(item => item.Id.ToString())
                .ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception exception)
        {
            logger.Warning($"Army of Two: pre-raid mask snapshot failed: {exception.Message}");
            return [];
        }
    }

    private void AddExtractedMaskHeat(
        VendettaProfileState state,
        IEnumerable<Item> items,
        string templateId,
        string ownerName)
    {
        var extracted = items.Any(item =>
            IsMask(item, templateId) &&
            item.Upd?.SpawnedInSession == true &&
            !state.PreRaidMaskItemIds.Contains(item.Id.ToString()));
        if (!extracted)
        {
            return;
        }

        var previousHeat = state.Heat;
        state.Heat = Math.Min(GetHeatLimit(), state.Heat + 4);
        logger.Info(
            $"Army of Two: {ownerName}'s mask was extracted; Heat {previousHeat} -> {state.Heat}.");
    }

    private void EnsureThreatMessage(MongoId sessionId, VendettaProfileState state)
    {
        if (!state.Marked || state.ThreatMessageSent)
        {
            return;
        }

        var sender = CreateRookSender();

        try
        {
            EnsureRookDialogue(sessionId, sender);
            mailSendService.SendUserMessageToPlayer(sessionId, sender, ThreatMessage, [], null);
            state.ThreatMessageSent = true;
            logger.Warning("Army of Two: player marked; Rook's threat message was sent.");
        }
        catch (Exception exception)
        {
            logger.Error($"Army of Two: could not send Rook's threat message: {exception.Message}");
        }
    }

    private void RepairRookDialogue(MongoId sessionId, bool createIfMissing)
    {
        var sender = CreateRookSender();
        var profile = saveServer.GetProfile(sessionId);
        if (profile == null)
        {
            return;
        }

        if (!createIfMissing &&
            (profile.DialogueRecords == null || !profile.DialogueRecords.ContainsKey(sender.Id)))
        {
            return;
        }

        EnsureRookDialogue(sessionId, sender);
    }

    private void EnsureRookDialogue(MongoId sessionId, UserDialogInfo sender)
    {
        var profile = saveServer.GetProfile(sessionId);
        var pmc = profile?.CharacterData?.PmcData;
        if (profile == null || pmc?.Info == null)
        {
            return;
        }

        profile.DialogueRecords ??= new Dictionary<MongoId, Dialogue>();
        if (!profile.DialogueRecords.TryGetValue(sender.Id, out var dialogue))
        {
            dialogue = new Dialogue
            {
                AttachmentsNew = 0,
                New = 0,
                Type = MessageType.UserMessage,
                Pinned = false,
                Messages = [],
                Id = sender.Id
            };
            profile.DialogueRecords[sender.Id] = dialogue;
        }

        dialogue.Users =
        [
            sender,
            new UserDialogInfo
            {
                Id = pmc.Id ?? sessionId,
                Aid = pmc.Aid ?? profile.ProfileInfo?.Aid,
                Info = new UserDialogDetails
                {
                    Nickname = pmc.Info.Nickname,
                    Side = pmc.Info.Side,
                    Level = pmc.Info.Level,
                    MemberCategory = pmc.Info.MemberCategory,
                    SelectedMemberCategory = pmc.Info.SelectedMemberCategory
                }
            }
        ];
    }

    private static UserDialogInfo CreateRookSender() => new()
    {
        Id = new MongoId(RookSenderId),
        Aid = 0,
        Info = new UserDialogDetails
        {
            Nickname = "Rook",
            Side = "Savage",
            Level = 70,
            MemberCategory = MemberCategory.Emissary,
            SelectedMemberCategory = MemberCategory.Emissary
        }
    };

    private void MarkIfThresholdReached(VendettaProfileState state)
    {
        var heatLimit = GetHeatLimit();
        if (state.Marked || state.Heat < heatLimit)
        {
            return;
        }

        state.Heat = heatLimit;
        state.Marked = true;
    }

    private static void ResetRaidState(VendettaProfileState state)
    {
        state.ActiveRaidId = string.Empty;
        state.HuntActive = false;
        state.RookEventLevel = VendettaEventLevel.None;
        state.TombstoneEventLevel = VendettaEventLevel.None;
        state.RookKilledDuringHunt = false;
        state.TombstoneKilledDuringHunt = false;
        state.PreRaidMaskItemIds.Clear();
    }

    private void Normalize(VendettaProfileState state)
    {
        state.Heat = Math.Clamp(state.Heat, 0, GetHeatLimit());

        state.ActiveRaidId ??= string.Empty;
        state.LastProcessedRaidId ??= string.Empty;
        state.PreRaidMaskItemIds ??= [];
    }

    private int GetHeatLimit() => Math.Clamp(configService.Load().Spawns.HeatLimit, 10, 100);

    private static VendettaEventLevel ParseEventLevel(string value)
    {
        if (string.Equals(value, "encounter", StringComparison.OrdinalIgnoreCase))
        {
            return VendettaEventLevel.Encounter;
        }

        if (string.Equals(value, "damage", StringComparison.OrdinalIgnoreCase))
        {
            return VendettaEventLevel.Damage;
        }

        if (string.Equals(value, "kill", StringComparison.OrdinalIgnoreCase))
        {
            return VendettaEventLevel.Kill;
        }

        return VendettaEventLevel.None;
    }

    private static int GetHeat(VendettaEventLevel level) => level switch
    {
        VendettaEventLevel.Encounter => 2,
        VendettaEventLevel.Damage => 3,
        VendettaEventLevel.Kill => 6,
        _ => 0
    };

    private static bool IsMask(Item item, string templateId) =>
        string.Equals(item.Template.ToString(), templateId, StringComparison.OrdinalIgnoreCase);

    private string GetCurrentLocation(MongoId sessionId) =>
        currentStatuses.TryGetValue(sessionId.ToString(), out var status)
            ? status.Location
            : string.Empty;

    private VendettaRaidStatus CreateStatus(VendettaProfileState state, string? location) => new()
    {
        Heat = state.Heat,
        Marked = state.Marked,
        HuntActive = state.HuntActive,
        DirectHuntActive = state.HuntActive && configService.Load().Spawns.DirectHuntEnabled,
        Location = location ?? string.Empty
    };
}
