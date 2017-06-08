﻿using KSPPluginFramework;
using System;
using System.Collections.Generic;
using UnityEngine;
using KSP;
using System.ComponentModel;
using KSP.UI;
using KSP.UI.Screens;

namespace HLAirships
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class HLEnvelopeControlWindowFlight : HLEnvelopeControlWindow
	{
		public override string MonoName { get { return this.name; } }
		//public override bool ViewAlarmsOnly { get { return false; } }
	}

	/// <summary>
	/// Have to do this behaviour or some of the textures are unloaded on first entry into flight mode
	/// </summary>
	[KSPAddon(KSPAddon.Startup.MainMenu, false)]
	public class InitialAirshipsTextureLoader : MonoBehaviour
	{
		//Awake Event - when the DLL is loaded
		public void Awake()
		{
			AirshipResources.loadGUIAssets();
		}
	}

	internal enum ButtonStyleEnum
	{
		[Description("Basic button")]
		Basic,
		[Description("Common Toolbar (by Blizzy78)")]
		Toolbar,
		[Description("KSP App Launcher Button")]
		Launcher,
	}

	public class HLEnvelopeControlWindow : MonoBehaviourExtended
	{

		public bool ControlWindowVisible { get; set; }
		public virtual String MonoName { get; set; }
		public static HLEnvelopeControlWindow Instance { get; set; }
		public float TargetBuoyantVessel { get; set; }
		public float TargetVerticalVelocity { get; set; }
		public bool ToggleAltitudeControl { get; set; }
		public bool ToggleAutoPitch { get; set; }
		public float TotalBuoyancy { get; set; }
		public Vessel CurrentVessel { get; set; }
		public float MakeStationarySpeedMax { get; set; }
		public Vector3 MaxBuoyancy { get; set; }
		public bool DisplayHologram { get; set; }
		public float LineOffsetMultiplier { get; set; }
		public bool AnchorPresent { get; set; }
		public bool AnchorOn { get; set; }
		public bool AutoAnchor { get; set; }

		public List<HLEnvelopePartModule> Envelopes = new List<HLEnvelopePartModule>();

		private Rect windowPos;
		private int airshipWindowID;
		private bool activeGUI;
		private float windowWidth;
		private bool resetGUIsize = false;
		private bool willReset1 = false;
		private bool willReset2 = false;
		// private bool willReset3 = false;
		private bool willReset4 = false;

		internal override void Awake()
		{
			Instance = this;
			GameEvents.onGUIApplicationLauncherReady.Add(OnGUIAppLauncherReady);
			GameEvents.onGUIApplicationLauncherDestroyed.Add(DestroyAppLauncherButton);
			GameEvents.onGameSceneLoadRequested.Add(OnGameSceneLoadRequestedForAppLauncher);

			GameEvents.onShowUI.Add(OnShowUI);
			GameEvents.onHideUI.Add(OnHideUI);
			InitVariables();
		}
		private void InitVariables()
		{
			airshipWindowID = UnityEngine.Random.Range(1000, 2000000) + _AssemblyName.GetHashCode();
		}

		private void OnShowUI()
		{
			LogFormatted_DebugOnly("OnShowGUI Fired");
		}
		private void OnHideUI()
		{
			LogFormatted_DebugOnly("OnHideGUI Fired");
		}

		internal override void OnGUIEvery()
		{
			if (ControlWindowVisible)
			{
				if(!activeGUI)
				{
					initGUI();
					activeGUI = true;
				}

				drawGUI();
			}
			else
			{
				if (activeGUI)
				{
					activeGUI = false;
				}
			}
		}
		/// <summary>
		/// Sets up the App Button - no longer called by the event as that only happens on StartMenu->SpaceCenter now
		/// </summary>
		void OnGUIAppLauncherReady()
		{
			MonoBehaviourExtended.LogFormatted_DebugOnly("AppLauncherReady");
			if (ApplicationLauncher.Ready)
			{
				if (btnAppLauncher == null)
				{
					btnAppLauncher = InitAppLauncherButton();
				}
			}
			else { LogFormatted("App Launcher-Not Actually Ready"); }
		}

		void OnGameSceneLoadRequestedForAppLauncher(GameScenes SceneToLoad)
		{
			LogFormatted_DebugOnly("GameSceneLoadRequest");
			DestroyAppLauncherButton();
		}
		internal ApplicationLauncherButton btnAppLauncher = null;

		internal ApplicationLauncherButton InitAppLauncherButton()
		{
			ApplicationLauncherButton retButton = null;

			ApplicationLauncherButton[] lstButtons = FindObjectsOfType<ApplicationLauncherButton>();
			//LogFormatted("AppLauncher: Creating Button-BEFORE", lstButtons.Length);
			try
			{
				retButton = ApplicationLauncher.Instance.AddModApplication(
					onAppLaunchToggleOn, onAppLaunchToggleOff,
					onAppLaunchHoverOn, onAppLaunchHoverOff,
					null, null,
					ApplicationLauncher.AppScenes.FLIGHT,
					(Texture)AirshipResources.iconAirshipControlWindow);
			}
			catch (Exception ex)
			{
				MonoBehaviourExtended.LogFormatted("AppLauncher: Failed to set up App Launcher Button\r\n{0}", ex.Message);
				retButton = null;
			}
			lstButtons = FindObjectsOfType<ApplicationLauncherButton>();
			//LogFormatted("AppLauncher: Creating Button-AFTER", lstButtons.Length);

			return retButton;
		}


		internal void DestroyAppLauncherButton()
		{
			//LogFormatted("AppLauncher: Destroying Button-BEFORE NULL CHECK");
			if (btnAppLauncher != null)
			{
				ApplicationLauncherButton[] lstButtons = FindObjectsOfType<ApplicationLauncherButton>();
				LogFormatted("AppLauncher: Destroying Button-Button Count:{0}", lstButtons.Length);
				ApplicationLauncher.Instance.RemoveModApplication(btnAppLauncher);
				btnAppLauncher = null;
			}
			//LogFormatted("AppLauncher: Destroying Button-AFTER NULL CHECK");
		}


		internal Boolean AppLauncherToBeSetTrue = false;
		internal DateTime AppLauncherToBeSetTrueAttemptDate;
		internal void SetAppButtonToTrue()
		{
			if (!ApplicationLauncher.Ready)
			{
				LogFormatted_DebugOnly("not ready yet");
				AppLauncherToBeSetTrueAttemptDate = DateTime.Now;
				return;
			}
			ApplicationLauncherButton ButtonToToggle = btnAppLauncher;

			if (ButtonToToggle == null)
			{
				LogFormatted_DebugOnly("Button Is Null");
				AppLauncherToBeSetTrueAttemptDate = DateTime.Now;
				return;
			}

		}

		void onAppLaunchToggleOn()
		{
			if (CurrentVessel != null)
			{
				MonoBehaviourExtended.LogFormatted_DebugOnly("TOn");

				ControlWindowVisible = true;
				MonoBehaviourExtended.LogFormatted_DebugOnly("{0}", ControlWindowVisible);
			}
		}
		void onAppLaunchToggleOff()
		{
			MonoBehaviourExtended.LogFormatted_DebugOnly("TOff");

			ControlWindowVisible = false;
			MonoBehaviourExtended.LogFormatted_DebugOnly("{0}", ControlWindowVisible);
		}
		void onAppLaunchHoverOn()
		{
			MonoBehaviourExtended.LogFormatted_DebugOnly("HovOn");
			//MouseOverAppLauncherBtn = true;
		}
		void onAppLaunchHoverOff()
		{
			MonoBehaviourExtended.LogFormatted_DebugOnly("HovOff");
			//MouseOverAppLauncherBtn = false; 
		}

		//Destroy Event - when the DLL is loaded
		internal override void OnDestroy()
		{
			LogFormatted("Destroying the KerbalAlarmClock-{0}", MonoName);

			//Hook the App Launcher
			GameEvents.onGUIApplicationLauncherReady.Remove(OnGUIAppLauncherReady);
			GameEvents.onGUIApplicationLauncherDestroyed.Remove(DestroyAppLauncherButton);
			GameEvents.onGameSceneLoadRequested.Remove(OnGameSceneLoadRequestedForAppLauncher);

			GameEvents.onShowUI.Remove(OnShowUI);
			GameEvents.onHideUI.Remove(OnHideUI);


			DestroyAppLauncherButton();
		}

		private void drawGUI()
		{
			windowPos = GUILayout.Window(airshipWindowID, windowPos, WindowGUI, "HLAirships", GUILayout.MinWidth(200));
		}

		protected void initGUI()
		{
			windowWidth = 300;

			if ((windowPos.x == 0) && (windowPos.y == 0))
			{
				windowPos = new Rect(Screen.width - windowWidth, Screen.height * 0.25f, 10, 10);
			}
		}

		// GUI
		private void WindowGUI(int windowID)
		{
			float targetBoyantForceFractionCompressorTemp;
			targetBoyantForceFractionCompressorTemp = TargetBuoyantVessel;

			float targetVerticalVelocityTemp;
			targetVerticalVelocityTemp = TargetVerticalVelocity;


			#region General GUI
			// General GUI window information
			GUIStyle mySty = new GUIStyle(GUI.skin.button);
			mySty.normal.textColor = mySty.focused.textColor = Color.white;
			mySty.hover.textColor = mySty.active.textColor = Color.yellow;
			mySty.onNormal.textColor = mySty.onFocused.textColor = mySty.onHover.textColor = mySty.onActive.textColor = Color.green;
			mySty.padding = new RectOffset(2, 2, 2, 2);

			// Buoyancy -, current %, and + buttons
			GUILayout.BeginHorizontal();
			if (GUILayout.RepeatButton("-", mySty))
			{
				TargetBuoyantVessel -= 0.002f;
				ToggleAltitudeControl = false;
			}
			TargetBuoyantVessel = Mathf.Clamp01(TargetBuoyantVessel);

			GUILayout.Label("        " + Mathf.RoundToInt(TargetBuoyantVessel * 100) + "%");

			if (GUILayout.RepeatButton("+", mySty))
			{
				TargetBuoyantVessel += 0.002f;
				ToggleAltitudeControl = false;
			}
			GUILayout.EndHorizontal();

			// Slider control.  Also is set by the other controls.
			GUILayout.BeginHorizontal();
			float temp = TargetBuoyantVessel;
			TargetBuoyantVessel = GUILayout.HorizontalSlider(TargetBuoyantVessel, 0f, 1f);
			if (temp != TargetBuoyantVessel)
			{
				ToggleAltitudeControl = false;
			}
			GUILayout.EndHorizontal();

			TargetBuoyantVessel = Mathf.Clamp01(TargetBuoyantVessel);
			#endregion

			#region Toggle Altitude
			// Altitude control.  Should be deactivated when pressing any other unrelated control.
			GUILayout.BeginHorizontal();
			string toggleAltitudeControlString = "Altitude Control Off";
			if (ToggleAltitudeControl) toggleAltitudeControlString = "Altitude Control On";
			ToggleAltitudeControl = GUILayout.Toggle(ToggleAltitudeControl, toggleAltitudeControlString);
			GUILayout.EndHorizontal();
			#endregion

			if (ToggleAltitudeControl)
			{
				#region Altitude Control
				willReset1 = true;

				// Vertical Velocity -, target velocity, and + buttons
				GUILayout.BeginHorizontal();
				GUILayout.Label("Target Vertical Velocity");
				GUILayout.EndHorizontal();

				GUILayout.BeginHorizontal();
				if (GUILayout.RepeatButton("--", mySty)) TargetVerticalVelocity -= 0.1f;
				if (GUILayout.Button("-", mySty)) TargetVerticalVelocity -= 0.1f;
				if (GUILayout.Button(TargetVerticalVelocity.ToString("00.0") + " m/s", mySty)) TargetVerticalVelocity = 0;
				if (GUILayout.Button("+", mySty)) TargetVerticalVelocity += 0.1f;
				if (GUILayout.RepeatButton("++", mySty)) TargetVerticalVelocity += 0.1f;
				GUILayout.EndHorizontal();
				#endregion

			}
			else
			{
				TargetVerticalVelocity = 0;
				if (willReset1)
				{
					resetGUIsize = true;
					willReset1 = false;
				}
			}

			if (Envelopes.Count > 1)
			{

				GUILayout.BeginHorizontal();
				string toggleAutoPitchString = "Auto Pitch Off";
				if (ToggleAutoPitch)
				{
					toggleAutoPitchString = "Auto Pitch On";
				}
				ToggleAutoPitch = GUILayout.Toggle(ToggleAutoPitch, toggleAutoPitchString);
				GUILayout.EndHorizontal();
			}

			if (ToggleAutoPitch)
			{
				willReset4 = true;
			}
			else
			{
				if (willReset4)
				{
					resetGUIsize = true;
					willReset4 = false;
				}
			}

			if (ToggleAutoPitch)
			{
				willReset2 = true;

#if DEBUG
				DisplayHologram = GUILayout.Toggle(DisplayHologram, "Display Hologram at " + LineOffsetMultiplier.ToString("F1"));
				if (DisplayHologram)
				{
					LineOffsetMultiplier = GUILayout.HorizontalSlider(LineOffsetMultiplier, -20f, 20f);
				}
#endif
			}
			else
			{
				DisplayHologram = false;
				if (willReset2)
				{
					resetGUIsize = true;
					willReset2 = false;
				}
			}

			if (AnchorPresent)
			{
				GUILayout.BeginHorizontal();
				string toggleAnchor = "Anchor Inactive";
				if (AnchorOn) toggleAnchor = "Anchor Active";
				AnchorOn = GUILayout.Toggle(AnchorOn, toggleAnchor);
				string toggleAutoAnchor = "Auto Anchor Off";
				if (AutoAnchor) toggleAutoAnchor = "Auto Anchor On";
				AutoAnchor = GUILayout.Toggle(AutoAnchor, toggleAutoAnchor);
				GUILayout.EndHorizontal();
			}

			if (resetGUIsize)
			{
				// Reset window size
				windowPos.Set(windowPos.x, windowPos.y, 10, 10);
				resetGUIsize = false;
			}

			#region Debug
			// Debug info


			GUILayout.BeginHorizontal();
			GUILayout.Label("Buoyancy - Weight: " + (TotalBuoyancy - (CurrentVessel.GetTotalMass() * FlightGlobals.getGeeForceAtPosition(CurrentVessel.GetWorldPos3D()).magnitude)).ToString("0.00"));
			GUILayout.EndHorizontal();

			//GUILayout.BeginHorizontal();
			//GUILayout.Label("Angle from Up: " + (ContAngle(heading, up, up)).ToString("0.0"));
			//GUILayout.EndHorizontal();

			//GUILayout.BeginHorizontal();
			//GUILayout.Label("Front Torque: " + (totalTorqueP).ToString("0.0"));
			//GUILayout.EndHorizontal();

			//GUILayout.BeginHorizontal();
			//GUILayout.Label("Rear Torque: " + (totalTorqueN).ToString("0.0"));
			//GUILayout.EndHorizontal();

			//GUILayout.BeginHorizontal();
			//GUILayout.Label("Front B: " + (targetBuoyancyP).ToString("0.00"));
			//GUILayout.EndHorizontal();

			//GUILayout.BeginHorizontal();
			//GUILayout.Label("Rear B: " + (targetBuoyancyN).ToString("0.00"));
			//GUILayout.EndHorizontal();

			//int x = 0;
			//foreach (HLEnvelopePart envelope in Envelopes)
			//{
			//    GUILayout.BeginHorizontal();
			//    GUILayout.Label("Env" + x + " Location: " + (envelope.eDistanceFromCoM).ToString("0.00"));
			//    GUILayout.EndHorizontal();
			//    GUILayout.BeginHorizontal();
			//    GUILayout.Label("Env" + x + " Buoyancy: " + (envelope.buoyantForce.magnitude).ToString("0.00"));
			//    GUILayout.EndHorizontal();
			//    GUILayout.BeginHorizontal();
			//    GUILayout.Label("Env" + x + " Specific Volume: " + (envelope.specificVolumeFractionEnvelope).ToString("0.00"));
			//    GUILayout.EndHorizontal();
			//    GUILayout.BeginHorizontal();
			//    GUILayout.Label("Env" + x + " targetPitchBuoyancy: " + (envelope.targetPitchBuoyancy).ToString("0.00"));
			//    GUILayout.EndHorizontal();
			//    GUILayout.BeginHorizontal();
			//    GUILayout.Label("Env" + x + " targetBoyantForceFractionCompressor: " + (envelope.targetBoyantForceFractionCompressor).ToString("0.00"));
			//    GUILayout.EndHorizontal();

			//    x += 1;
			//}

			#endregion

			GUI.DragWindow(new Rect(0, 0, 500, 20));
		}


	}
}
