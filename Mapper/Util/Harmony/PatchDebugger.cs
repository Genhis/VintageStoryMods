namespace Mapper.Util.Harmony;

using HarmonyLib;
using Mapper.Util.Reflection;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;

public class PatchDebugger {
	private readonly string modOwnerID;
	private readonly ILogger logger;
	private readonly bool debugMode;
	private readonly HashSet<string> conflictingHarmonyIDs = [];
	private int conflictingPatchCount;

	// Used by HasAnyConflict(), put here for convenience.
	private bool patchHasOwner;
	private bool patchHasConflict;

	private PatchDebugger(string modOwnerID, ILogger logger, bool debugMode) {
		this.modOwnerID = modOwnerID;
		this.logger = logger;
		this.debugMode = debugMode;
	}

	private void Run() {
		foreach(MethodBase method in Harmony.GetAllPatchedMethods()) {
			Patches patches = Harmony.GetPatchInfo(method);
			if(this.debugMode) {
				this.logger.VerboseDebug($"Patches of `{method.GetParentAndName()}`:");
				PatchDebugger.ForEachPatch(patches, this.PrintPatchInfo);
			}
			if(this.HasAnyConflict(patches))
				PatchDebugger.ForEachPatch(patches, this.ProcessPatchConflict);
		}

		if(this.conflictingPatchCount > 0)
			this.logger.Notification($"Found {this.conflictingPatchCount} potentially conflicting mod patches from {this.conflictingHarmonyIDs.Count} mods: {string.Join(", ", this.conflictingHarmonyIDs)}");
		else
			this.logger.Notification("No conflicting mod patches found");
	}

	private void PrintPatchInfo(IEnumerable<Patch> patches, string type) {
		foreach(Patch patch in patches)
			this.logger.VerboseDebug($"  [{type}][{patch.owner}] {patch.PatchMethod.GetParentAndName()} # priority = {patch.priority}, before = [{string.Join(", ", patch.before)}], after = [{string.Join(", ", patch.after)}]");
	}

	private void ProcessPatchConflict(IEnumerable<Patch> patches, string type) {
		foreach(Patch patch in patches)
			if(patch.owner != this.modOwnerID) {
				this.conflictingHarmonyIDs.Add(patch.owner);
				++this.conflictingPatchCount;
			}
	}

	private bool HasAnyConflict(Patches patches) {
		this.patchHasOwner = false;
		this.patchHasConflict = false;
		return this.HasAnyConflict(patches.Prefixes) || this.HasAnyConflict(patches.Postfixes) || this.HasAnyConflict(patches.Transpilers) || this.HasAnyConflict(patches.Finalizers);
	}

	private bool HasAnyConflict(IEnumerable<Patch> patches) {
		foreach(Patch patch in patches) {
			if(patch.owner == this.modOwnerID)
				this.patchHasOwner = true;
			else
				this.patchHasConflict = true;
		}
		return this.patchHasOwner & this.patchHasConflict;
	}

	public static void CheckPatchConflicts(string modOwnerID, ILogger logger, bool debugMode) {
		new PatchDebugger(modOwnerID, logger, debugMode).Run();
	}

	private static void ForEachPatch(Patches patches, System.Action<IEnumerable<Patch>, string> action) {
		action(patches.Prefixes, "prefix");
		action(patches.Postfixes, "postfix");
		action(patches.Transpilers, "transpiler");
		action(patches.Finalizers, "finalizer");
	}
}
