using System.Text.Json;
using System.Globalization;
namespace SharpTestBench.Tools;
/// <summary>Independent source-reviewed expected identities and ordinal seeds for local protocol qualification.</summary>
internal static class ProtocolContractInventory
{
    internal static readonly IReadOnlyList<string> Names = Array.AsReadOnly<string>(
    [
        "Entry.R001_ZeroBytePacket",
        "Entry.R002_AllOnesBytePacket",
        "Entry.R003_PublishedCheckVector",
        "Entry.R004_MostSignificantBitPacket",
        "Entry.R005_LeastSignificantBitPacket",
        "Entry.R006_EachBitAsItsOwnPacket",
        "Entry.R007_AllByteValuesOnePacket",
        "Entry.R008_ZeroRunKeepsPolynomialZero",
        "Entry.R009_OnesRunAccumulatesAcrossBytes",
        "Entry.R010_AlternatingBitOrder",
        "Entry.R011_AdjacentPacketBoundariesResetAccumulator",
        "Entry.R012_SeededPacketLengthsAndPayloads",
        "Entry.R013_ContinuousAcceptedBurst",
        "Entry.R014_SourceGapsDoNotAdvancePacket",
        "Entry.R015_SinkBackpressureHoldsCompleteWord",
        "Entry.R016_IndependentSourceAndSinkGaps",
        "Entry.R017_LastWordIsHeldUnderBackpressure",
        "Entry.R018_LengthCrossesEightBitBoundary",
        "Entry.R019_ResetEmptyStage",
        "Entry.R020_ResetDiscardsFullStage",
        "Entry.R021_ResetOverridesStalledLast",
        "Entry.R022_BackToBackSingleBytePackets",
        "Entry.R023_InvalidLastCannotResetPacket",
        "Entry.R024_InvalidDataCannotEnterAccumulator",
        "Entry.R025_BlockedInputChangesCannotCorruptHeldOutput",
        "Entry.R026_CrcOracleRejectsWrongRemainder",
        "Entry.R027_DataOracleRejectsCorruptedPayload",
        "Entry.R028_LengthOracleRejectsCountingBubbles",
        "Entry.R029_LastOracleRejectsBoundaryLoss",
        "Entry.R030_NegativeProbeDoesNotPoisonLaterPacket",
        "Entry.R031_OneValidPulseProducesExactlyOneDelivery",
        "Entry.R032_DifferentPacketLengthsRestartAtOne",
        "Entry.R033_NoRequestProducesNoGrant",
        "Entry.R034_PortZeroCanWinAlone",
        "Entry.R035_PortOneCanWinAlone",
        "Entry.R036_PortTwoCanWinAlone",
        "Entry.R037_PortThreeCanWinAlone",
        "Entry.R038_AllRequestsRotateInLiteralOrder",
        "Entry.R039_TwoContendersAlternate",
        "Entry.R040_LastPortWrapsToPortZero",
        "Entry.R041_NoAcknowledgeKeepsPriority",
        "Entry.R042_DisableSuppressesAllGrants",
        "Entry.R043_DisabledAcknowledgeCannotRotate",
        "Entry.R044_EmptyAcknowledgementCannotRotate",
        "Entry.R045_RequestWithdrawalReselectsWithoutOwnerLock",
        "Entry.R046_LateRequestUsesCurrentPriority",
        "Entry.R047_AllRequestMasksAtEveryPointer",
        "Entry.R048_SeededRequestEnableAckInterleaving",
        "Entry.R049_PersistentRequestsBoundServiceToFourAcks",
        "Entry.R050_ResetRestartsAtPortZero",
        "Entry.R051_ResetDominatesAcknowledgement",
        "Entry.R052_NativePriorityFaultIsDetectedAndRestored",
        "Entry.R053_ResetClearsEveryWordAndOutput",
        "Entry.R054_FullWordWriteAtEveryAddress",
        "Entry.R055_LowByteWritePreservesHighLane",
        "Entry.R056_HighByteWritePreservesLowLane",
        "Entry.R057_ZeroMaskCannotChangeStoredWord",
        "Entry.R058_WriteDisableDominatesAllByteEnables",
        "Entry.R059_ReadDisableHoldsPriorOutput",
        "Entry.R060_ReadIsSynchronousRatherThanCombinational",
        "Entry.R061_SameAddressCollisionReadsOldWord",
        "Entry.R062_WritingOneAddressCannotModifyNeighbors",
        "Entry.R063_ResetDominatesConcurrentReadAndWrite",
        "Entry.R064_ResetClearsHeldReadOutput",
        "Entry.R065_AlternatingLaneUpdatesComposeOneWord",
        "Entry.R066_SeededMixedBankOperations",
        "Entry.R067_FirstAndLastAddressesDoNotAlias",
        "Entry.R068_ZeroAndOnesLaneDataCanOverwriteEveryBit",
        "Entry.R069_OracleRejectsSwappedByteLanes",
        "Entry.R070_AfterNegativeProbeLaterWriteAndReadRecover"
    ]);
    internal static Dictionary<string,ulong> Select(JsonElement discovery,int through,ulong baseSeed,int available=70)
    {
        if(available<1||available>Names.Count||through<1||through>available)throw new ArgumentOutOfRangeException(nameof(available));
        var selected=ContractsCiCommand.SelectForIcarus(discovery,through,available);
        // A selected prefix does not authorize missing or extra discovered cases.
        // The caller binds available to the compiled fixture milestone, not producer rows.
        var observed=discovery.GetProperty("Tests").EnumerateArray().Select(ParityContracts.Identity).ToHashSet(StringComparer.Ordinal);
        if(!observed.SetEquals(Names.Take(available)))throw new InvalidDataException("Protocol complete discovery inventory differs from source milestone");
        foreach(var row in discovery.GetProperty("Tests").EnumerateArray())
        {
            string name=ParityContracts.Identity(row);int index=int.Parse(name.AsSpan(7,3),CultureInfo.InvariantCulture)-1;
            if(index<0||index>=Names.Count||Names[index]!=name||row.GetProperty("Seed").GetUInt64()!=unchecked(baseSeed+(ulong)index)||row.GetProperty("Stage").GetInt32()!=0||row.GetProperty("CaseIndex").GetInt32()!=0)
                throw new InvalidDataException("Protocol discovery differs from independent source inventory/ordinal seed");
        }
        if(!selected.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(Names.Take(through)))throw new InvalidDataException("Protocol selected identities differ from source inventory");
        return selected;
    }
}