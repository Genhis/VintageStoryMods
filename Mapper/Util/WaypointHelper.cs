namespace Mapper.Util;

using System;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

/// <summary>
/// Helper class to interact with the game's WaypointMapLayer via reflection.
/// </summary>
public static class WaypointHelper {
	private static FieldInfo? waypointsField;
	private static MethodInfo? resendWaypointsMethod;
	private static MethodInfo? rebuildMethod;
	private static bool initialized;
	private static bool available;

	/// <summary>
	/// Initializes reflection access to WaypointMapLayer internals.
	/// </summary>
	private static bool Initialize(ICoreAPI api) {
		if(initialized)
			return available;

		initialized = true;

		try {
			Type waypointMapLayerType = typeof(WaypointMapLayer);

			// Find the Waypoints field (List<Waypoint>)
			waypointsField = waypointMapLayerType.GetField("Waypoints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if(waypointsField == null) {
				api.Logger.Warning("[mapper] Could not find WaypointMapLayer.Waypoints field");
				return false;
			}

			// Find ResendWaypoints method to notify clients
			resendWaypointsMethod = waypointMapLayerType.GetMethod("ResendWaypoints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

			// Find RebuildMapComponents method
			rebuildMethod = waypointMapLayerType.GetMethod("RebuildMapComponents", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

			available = true;
			return true;
		}
		catch(Exception ex) {
			api.Logger.Warning($"[mapper] Failed to initialize WaypointHelper: {ex.Message}");
			return false;
		}
	}

	/// <summary>
	/// Gets the WaypointMapLayer from the WorldMapManager.
	/// </summary>
	public static WaypointMapLayer? GetWaypointMapLayer(ICoreServerAPI sapi) {
		try {
			WorldMapManager mapManager = sapi.ModLoader.GetModSystem<WorldMapManager>();
			FieldInfo? mapLayersField = typeof(WorldMapManager).GetField("MapLayers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if(mapLayersField == null)
				return null;

			object? mapLayers = mapLayersField.GetValue(mapManager);
			if(mapLayers is not System.Collections.IEnumerable layers)
				return null;

			foreach(object layer in layers) {
				if(layer is WaypointMapLayer waypointLayer)
					return waypointLayer;
			}
		}
		catch {
			// Ignore errors
		}
		return null;
	}

	/// <summary>
	/// Gets all waypoints for a specific player from WaypointMapLayer.
	/// </summary>
	public static List<Waypoint> GetPlayerWaypoints(ICoreServerAPI sapi, string playerUid) {
		List<Waypoint> result = [];

		if(!Initialize(sapi))
			return result;

		try {
			WaypointMapLayer? waypointLayer = GetWaypointMapLayer(sapi);
			if(waypointLayer == null)
				return result;

			object? waypointsObj = waypointsField!.GetValue(waypointLayer);
			if(waypointsObj is not List<Waypoint> waypoints)
				return result;

			foreach(Waypoint wp in waypoints) {
				if(wp.OwningPlayerUid == playerUid)
					result.Add(wp);
			}
		}
		catch(Exception ex) {
			sapi.Logger.Warning($"[mapper] Failed to get player waypoints: {ex.Message}");
		}

		return result;
	}

	/// <summary>
	/// Replaces all of a player's waypoints with the provided list.
	/// Removes existing waypoints for the player and adds the new ones.
	/// </summary>
	/// <returns>Number of waypoints that changed (added, removed, or modified).</returns>
	public static int ReplacePlayerWaypoints(ICoreServerAPI sapi, IServerPlayer player, Dictionary<string, Waypoint> newWaypoints) {
		if(!Initialize(sapi))
			return 0;

		int changes = 0;

		try {
			WaypointMapLayer? waypointLayer = GetWaypointMapLayer(sapi);
			if(waypointLayer == null)
				return 0;

			object? waypointsObj = waypointsField!.GetValue(waypointLayer);
			if(waypointsObj is not List<Waypoint> allWaypoints)
				return 0;

			Dictionary<string, Waypoint> playerWaypoints = [];
			foreach(Waypoint wp in allWaypoints) {
				if(!string.IsNullOrEmpty(wp.Guid) && wp.OwningPlayerUid == player.PlayerUID)
					playerWaypoints[wp.Guid] = wp;
			}

			foreach(Waypoint newWaypoint in newWaypoints.Values) {
				if(playerWaypoints.TryGetValue(newWaypoint.Guid, out Waypoint? playerWaypoint)) {
					if(playerWaypoint != newWaypoint) {
						playerWaypoint.Title = newWaypoint.Title;
						playerWaypoint.Text = newWaypoint.Text;
						playerWaypoint.Icon = newWaypoint.Icon;
						playerWaypoint.Color = newWaypoint.Color;
						playerWaypoint.Position = newWaypoint.Position;
						playerWaypoint.Pinned = newWaypoint.Pinned;
						playerWaypoint.ShowInWorld = newWaypoint.ShowInWorld;
						changes++;
					}
				}
				else {
					newWaypoint.OwningPlayerUid = player.PlayerUID;
					allWaypoints.Add(newWaypoint);
					changes++;
				}
			}

			// Notify client
			if(changes > 0)
				ResendAndRebuild(waypointLayer, player);
		}
		catch(Exception ex) {
			sapi.Logger.Warning($"[mapper] Failed to replace player waypoints: {ex.Message}");
		}

		return changes;
	}

	/// <summary>
	/// Resends waypoints to client and rebuilds map components.
	/// </summary>
	private static void ResendAndRebuild(WaypointMapLayer waypointLayer, IServerPlayer player) {
		resendWaypointsMethod?.Invoke(waypointLayer, [player]);
		rebuildMethod?.Invoke(waypointLayer, null);
	}
}
