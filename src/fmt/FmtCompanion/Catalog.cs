using System;
using System.Collections.Generic;
using System.Linq;

namespace FmtCompanion;

public static class Catalog
{
	public const string Car = "Voiture";

	public const string Pit = "Stand";

	public const string Camera = "Caméra";

	public const string Cockpit = "Poste";

	public static readonly string[] Families = new string[4] { "Voiture", "Stand", "Caméra", "Poste" };

	private static readonly string[] WheelNames = new string[4] { "avant gauche", "avant droit", "arrière gauche", "arrière droit" };

	private static IList<FmtFunction> all;

	public static readonly HashSet<string> LegacyCommands = new HashSet<string>(new string[27]
	{
		"Refuel.Minus10", "Refuel.Minus5", "Refuel.Minus1", "Refuel.Plus1", "Refuel.Plus5", "Refuel.Plus10", "Refuel.Minimum", "Refuel.Maximum", "Ratio.Plus", "Ratio.Minus",
		"Driver.Next", "Driver.Previous", "StopGo.Serve", "StopGo.Defer", "Tyres.All.Same", "Tyres.All.Next", "Tyres.All.Previous", "Tyres.All.None", "Repairs.Next", "Repairs.Previous",
		"Repairs.None", "Repairs.Bodywork", "Repairs.All", "Brakes.Next", "Brakes.Previous", "Brakes.Replace", "Brakes.Keep"
	}.Concat(Commands.Wheels.SelectMany((string w) => new string[8] { "Next", "Previous", "None", "Same", "Soft", "Medium", "Hard", "Wet" }.Select((string s) => "Tyres." + w + "." + s))).Concat(new string[4] { "Soft", "Medium", "Hard", "Wet" }.Select((string s) => "Tyres.All." + s)), StringComparer.Ordinal);

	public static IList<FmtFunction> All => all ?? (all = Build().AsReadOnly());

	public static FmtFunction Find(string id)
	{
		return All.FirstOrDefault((FmtFunction f) => f.Id == id);
	}

	private static List<FmtFunction> Build()
	{
		List<FmtFunction> list = new List<FmtFunction>();
		Action<string, string, string, string, string> key = delegate(string family, string group, string id, string label, string control)
		{
			list.Add(new FmtFunction
			{
				Id = "Key." + id,
				Family = family,
				Group = group,
				Label = label,
				Kind = FunctionKind.LmuKey,
				LmuControl = control,
				DefaultVisible = true
			});
		};
		Action<string, string, string, bool> action = delegate(string group, string command, string label, bool visible)
		{
			list.Add(new FmtFunction
			{
				Id = command,
				Family = "Stand",
				Group = group,
				Label = label,
				Kind = FunctionKind.Pit,
				PitCommand = command,
				DefaultVisible = visible
			});
		};
		string[][] array = new string[4][]
		{
			new string[3] { "TC", "TC", "Traction Control" },
			new string[3] { "TC2", "Coupure TC", "Traction Control 2" },
			new string[3] { "TCSlip", "TC glisse", "Traction Control Slip Angle" },
			new string[3] { "ABS", "ABS", "Antilock Brake System" }
		};
		foreach (string[] array2 in array)
		{
			key("Voiture", "Électronique", array2[0] + ".Previous", array2[1] + " précédent", array2[2] + " Down");
			key("Voiture", "Électronique", array2[0] + ".Next", array2[1] + " suivant", array2[2] + " Up");
		}
		key("Voiture", "Électronique", "MotorMap.Previous", "Carto moteur précédente", "Decrement Motor Map");
		key("Voiture", "Électronique", "MotorMap.Next", "Carto moteur suivante", "Increment Motor Map");
		key("Voiture", "Électronique", "Regen.Previous", "Régénération précédente", "Decrement Regeneration");
		key("Voiture", "Électronique", "Regen.Next", "Régénération suivante", "Increment Regeneration");
		key("Voiture", "Châssis", "FrontARB.Previous", "Antiroulis avant précédent", "Dec Front ARB");
		key("Voiture", "Châssis", "FrontARB.Next", "Antiroulis avant suivant", "Inc Front ARB");
		key("Voiture", "Châssis", "RearARB.Previous", "Antiroulis arrière précédent", "Dec Rear ARB");
		key("Voiture", "Châssis", "RearARB.Next", "Antiroulis arrière suivant", "Inc Rear ARB");
		key("Voiture", "Freinage", "Bias.Forward", "Répartition vers l'avant", "Bias Forward");
		key("Voiture", "Freinage", "Bias.Rearward", "Répartition vers l'arrière", "Bias Rearward");
		key("Voiture", "Freinage", "Migration.Forward", "Migration vers l'avant", "Brake Migration Forward");
		key("Voiture", "Freinage", "Migration.Rearward", "Migration vers l'arrière", "Brake Migration Rearward");
		key("Voiture", "Systèmes", "Limiter", "Limiteur", "Speed Limiter");
		key("Voiture", "Systèmes", "PitRequest", "Demande d'arrêt", "Pit Request");
		key("Voiture", "Systèmes", "Headlights", "Phares", "Headlights");
		key("Voiture", "Systèmes", "HeadlightsPulse", "Appel de phares", "Headlights Pulse");
		key("Voiture", "Systèmes", "Wipers", "Essuie-glace", "Wipers");
		action("Énergie", "Refuel.Previous", "Énergie précédente", arg4: true);
		action("Énergie", "Refuel.Next", "Énergie suivante", arg4: true);
		action("Énergie", "Refuel.Minus1", "Énergie -1", arg4: false);
		action("Énergie", "Refuel.Plus1", "Énergie +1", arg4: false);
		action("Énergie", "Refuel.Minus10", "Énergie -10", arg4: true);
		action("Énergie", "Refuel.Plus10", "Énergie +10", arg4: true);
		action("Énergie", "Refuel.Minimum", "Énergie minimum", arg4: true);
		action("Énergie", "Refuel.Maximum", "Énergie maximum", arg4: true);
		action("Ratio carburant", "Ratio.Minus", "Ratio précédent", arg4: true);
		action("Ratio carburant", "Ratio.Plus", "Ratio suivant", arg4: true);
		action("Pneus", "Tyres.All.Previous", "Pneus 4 roues précédent", arg4: true);
		action("Pneus", "Tyres.All.Next", "Pneus 4 roues suivant", arg4: true);
		string[][] array3 = new string[4][]
		{
			new string[2] { "Soft", "Soft neufs" },
			new string[2] { "Medium", "Medium neufs" },
			new string[2] { "Hard", "Hard neufs" },
			new string[2] { "Wet", "Pluie neufs" }
		};
		foreach (string[] array4 in array3)
		{
			action("Pneus", "Tyres.All." + array4[0], "Pneus 4 roues " + array4[1], arg4: true);
		}
		action("Pneus", "Tyres.All.Same", "Pneus 4 roues même gomme neuve", arg4: true);
		action("Pneus", "Tyres.All.None", "Pneus 4 roues aucun changement", arg4: true);
		action("Pneus", "Tyres.All.Cycle", "Pneus 4 roues gomme suivante en boucle", arg4: true);
		for (int k = 0; k < 4; k++)
		{
			string text = "Tyres." + Commands.Wheels[k] + ".";
			string text2 = "Pneu " + WheelNames[k];
			action("Pneus par roue", text + "Previous", text2 + " précédent", arg4: false);
			action("Pneus par roue", text + "Next", text2 + " suivant", arg4: false);
			string[][] array5 = new string[4][]
			{
				new string[2] { "Soft", "Soft neuf" },
				new string[2] { "Medium", "Medium neuf" },
				new string[2] { "Hard", "Hard neuf" },
				new string[2] { "Wet", "Pluie neuf" }
			};
			foreach (string[] array6 in array5)
			{
				action("Pneus par roue", text + array6[0], text2 + " " + array6[1], arg4: false);
			}
			action("Pneus par roue", text + "Same", text2 + " même gomme neuve", arg4: false);
			action("Pneus par roue", text + "None", text2 + " aucun changement", arg4: false);
			action("Pneus par roue", text + "Cycle", text2 + " gomme suivante en boucle", arg4: true);
		}
		action("Pressions", "Pressure.All.Previous", "Pressions 4 roues précédentes", arg4: true);
		action("Pressions", "Pressure.All.Next", "Pressions 4 roues suivantes", arg4: true);
		for (int m = 0; m < 4; m++)
		{
			action("Pressions", "Pressure." + Commands.Wheels[m] + ".Previous", "Pression " + WheelNames[m] + " précédente", arg4: false);
			action("Pressions", "Pressure." + Commands.Wheels[m] + ".Next", "Pression " + WheelNames[m] + " suivante", arg4: false);
		}
		action("Aéro et refroidissement", "Wing.Previous", "Aileron précédent", arg4: true);
		action("Aéro et refroidissement", "Wing.Next", "Aileron suivant", arg4: true);
		action("Aéro et refroidissement", "Grille.Previous", "Calandre précédente", arg4: true);
		action("Aéro et refroidissement", "Grille.Next", "Calandre suivante", arg4: true);
		action("Aéro et refroidissement", "BrakeDuct.Front.Previous", "Écopes avant précédentes", arg4: true);
		action("Aéro et refroidissement", "BrakeDuct.Front.Next", "Écopes avant suivantes", arg4: true);
		action("Aéro et refroidissement", "BrakeDuct.Rear.Previous", "Écopes arrière précédentes", arg4: true);
		action("Aéro et refroidissement", "BrakeDuct.Rear.Next", "Écopes arrière suivantes", arg4: true);
		action("Aéro et refroidissement", "Grille.Cycle", "Calandre suivante en boucle", arg4: true);
		action("Aéro et refroidissement", "BrakeDuct.Front.Cycle", "Écopes avant suivantes en boucle", arg4: true);
		action("Aéro et refroidissement", "BrakeDuct.Rear.Cycle", "Écopes arrière suivantes en boucle", arg4: true);
		action("Freins", "Brakes.Previous", "Freins précédent", arg4: false);
		action("Freins", "Brakes.Next", "Freins suivant", arg4: false);
		action("Freins", "Brakes.Replace", "Freins remplacer", arg4: true);
		action("Freins", "Brakes.Keep", "Freins conserver", arg4: true);
		action("Freins", "Brakes.Toggle", "Freins changer ou garder", arg4: true);
		action("Réparations", "Repairs.Previous", "Réparations précédent", arg4: false);
		action("Réparations", "Repairs.Next", "Réparations suivant", arg4: false);
		action("Réparations", "Repairs.None", "Réparations aucune", arg4: true);
		action("Réparations", "Repairs.Bodywork", "Réparations carrosserie", arg4: true);
		action("Réparations", "Repairs.All", "Réparations tout", arg4: true);
		action("Réparations", "Repairs.Cycle", "Réparations choix suivant en boucle", arg4: true);
		action("Pilote", "Driver.Previous", "Pilote précédent", arg4: true);
		action("Pilote", "Driver.Next", "Pilote suivant", arg4: true);
		action("Pilote", "Driver.Cycle", "Pilote suivant en boucle", arg4: true);
		action("Stop & Go", "StopGo.Previous", "Stop & Go précédent", arg4: false);
		action("Stop & Go", "StopGo.Next", "Stop & Go suivant", arg4: false);
		action("Stop & Go", "StopGo.Serve", "Stop & Go effectuer", arg4: true);
		action("Stop & Go", "StopGo.Defer", "Stop & Go ne pas effectuer", arg4: true);
		action("Stop & Go", "StopGo.Toggle", "Stop & Go effectuer ou reporter", arg4: true);
		Action<string, string, string, string> action2 = delegate(string group, string id, string label, string control)
		{
			key("Caméra", group, id, label, control);
			switch (group)
			{
			case "Caméra libre":
			case "Objectif libre":
			case "Replay":
				list[list.Count - 1].DefaultVisible = false;
				break;
			}
		};
		action2("Vues", "ChangeView", "Changer de vue", "Push To change camera view");
		action2("Vues", "Driving", "Conduite", "Driving Cameras");
		action2("Vues", "Onboard", "Embarquée", "Onboard Cameras");
		action2("Vues", "Tracking", "Poursuite", "Tracking Cameras");
		action2("Vues", "Spectator", "Spectateur", "Spectator Cameras");
		action2("Vues", "LookLeft", "Regard à gauche", "Look Left");
		action2("Vues", "LookRight", "Regard à droite", "Look Right");
		action2("Vues", "RearLook", "Regard arrière", "Rear Look");
		action2("Swingman", "Swingman", "Swingman", "Swingman Camera");
		string[][] array7 = new string[7][]
		{
			new string[2] { "Up", "haut" },
			new string[2] { "Down", "bas" },
			new string[2] { "Left", "gauche" },
			new string[2] { "Right", "droite" },
			new string[2] { "Zoom In", "zoom +" },
			new string[2] { "Zoom Out", "zoom -" },
			new string[2] { "Reset", "reset" }
		};
		foreach (string[] array8 in array7)
		{
			action2("Swingman", "Swingman." + array8[0].Replace(" ", ""), "Swingman " + array8[1], "Swingman " + array8[0]);
		}
		list.Add(new FmtFunction
		{
			Id = "Watch.Next",
			Family = "Caméra",
			Group = "TV",
			Label = "Voiture suivante",
			Kind = FunctionKind.Api,
			ApiPath = "rest/watch/focusForward",
			DefaultVisible = true
		});
		list.Add(new FmtFunction
		{
			Id = "Watch.Previous",
			Family = "Caméra",
			Group = "TV",
			Label = "Voiture précédente",
			Kind = FunctionKind.Api,
			ApiPath = "rest/watch/focusBackward",
			DefaultVisible = true
		});
		action2("TV", "ViewOriginal", "Revenir à ma voiture", "View Original Vehicle");
		action2("TV", "HoldTv", "Garder la caméra TV", "Hold TV Camera");
		action2("TV", "PauseTv", "Pause du suivi TV", "Pause TV Tracking");
		action2("TV", "TvZoom.In", "Zoom TV +", "Camera Zoom In");
		action2("TV", "TvZoom.Out", "Zoom TV -", "Camera Zoom Out");
		action2("TV", "TvZoom.Reset", "Zoom TV reset", "Camera Zoom Reset");
		action2("Caméra libre", "Free.Toggle", "Caméra libre on/off", "Toggle Free Move");
		action2("Caméra libre", "Free.Zero", "Caméra libre recentrer", "Zero Free Move");
		action2("Caméra libre", "Free.Mouse", "Caméra libre à la souris", "FreeLook Mouse Control Toggle");
		action2("Caméra libre", "Free.Slow", "Caméra libre lente", "Camera Slow Move");
		array = new string[12][]
		{
			new string[2] { "Move Forward", "avancer" },
			new string[2] { "Move Backward", "reculer" },
			new string[2] { "Move Left", "à gauche" },
			new string[2] { "Move Right", "à droite" },
			new string[2] { "Move Up", "monter" },
			new string[2] { "Move Down", "descendre" },
			new string[2] { "Tilt Up", "incliner vers le haut" },
			new string[2] { "Tilt Down", "incliner vers le bas" },
			new string[2] { "Tilt Left", "pencher à gauche" },
			new string[2] { "Tilt Right", "pencher à droite" },
			new string[2] { "Turn Left", "tourner à gauche" },
			new string[2] { "Turn Right", "tourner à droite" }
		};
		foreach (string[] array9 in array)
		{
			action2("Caméra libre", "Free." + array9[0].Replace(" ", ""), "Caméra libre " + array9[1], "Camera " + array9[0]);
		}
		action2("Objectif libre", "Lens.Toggle", "Objectif libre on/off", "Toggle Free Lens");
		action2("Objectif libre", "Lens.TrackingFocus", "Mise au point sur la voiture", "FreeLens Tracking Focus Toggle");
		array = new string[3][]
		{
			new string[2] { "Focus", "Mise au point" },
			new string[2] { "Aperture", "Ouverture" },
			new string[2] { "Exposure", "Exposition" }
		};
		foreach (string[] array10 in array)
		{
			action2("Objectif libre", "Lens." + array10[0] + ".Up", array10[1] + " +", "FreeLens " + array10[0] + " Increase");
			action2("Objectif libre", "Lens." + array10[0] + ".Down", array10[1] + " -", "FreeLens " + array10[0] + " Decrease");
		}
		action2("Replay", "InstantReplay", "Ralenti instantané", "Instant Replay");
		action2("Replay", "VehicleLabels", "Noms des voitures", "Display Vehicle Labels");
		array = new string[6][]
		{
			new string[2] { "Play", "lecture" },
			new string[2] { "Slowmotion", "ralenti" },
			new string[2] { "Fast Forward", "avance rapide" },
			new string[2] { "Fast Rewind", "retour rapide" },
			new string[2] { "Reverse", "lecture arrière" },
			new string[2] { "Stop", "stop" }
		};
		foreach (string[] array11 in array)
		{
			action2("Replay", "Replay." + array11[0].Replace(" ", ""), "Replay " + array11[1], "Replay " + array11[0]);
		}
		key("Poste", "Vue", "Fov.Up", "FOV vertical +", "Increase Vertical FOV");
		key("Poste", "Vue", "Fov.Down", "FOV vertical -", "Decrease Vertical FOV");
		key("Poste", "Vue", "Mirror", "Rétroviseur", "Toggle Mirror");
		key("Poste", "Siège", "Seat.Fore", "Siège avant", "Adjust Seat Fore");
		key("Poste", "Siège", "Seat.Aft", "Siège arrière", "Adjust Seat Aft");
		key("Poste", "Siège", "Seat.Up", "Siège haut", "Adjust Seat Up");
		key("Poste", "Siège", "Seat.Down", "Siège bas", "Adjust Seat Down");
		array = new string[6][]
		{
			new string[3] { "All", "Rétros tous", "Shift" },
			new string[3] { "Left", "Rétro gauche", "Ctrl" },
			new string[3] { "Right", "Rétro droit", "Alt" },
			new string[3] { "Centre", "Rétro central", "Ctrl+Alt" },
			new string[3] { "CentrePos", "Position rétro central", "Shift+Alt" },
			new string[3] { "Width", "Largeur des rétros", "Shift+Ctrl" }
		};
		foreach (string[] array12 in array)
		{
			array3 = new string[4][]
			{
				new string[3] { "Up", "haut", "Key.Seat.Up" },
				new string[3] { "Down", "bas", "Key.Seat.Down" },
				new string[3] { "Left", "gauche", "Key.Seat.Fore" },
				new string[3] { "Right", "droite", "Key.Seat.Aft" }
			};
			foreach (string[] array13 in array3)
			{
				list.Add(new FmtFunction
				{
					Id = "Mirror." + array12[0] + "." + array13[0],
					Family = "Poste",
					Group = "Rétroviseurs",
					Label = array12[1] + " " + array13[1],
					Kind = FunctionKind.Combo,
					Modifiers = array12[2],
					SeatId = array13[2],
					DefaultVisible = true
				});
			}
		}
		key("Poste", "Matériel", "Ffb.Reset", "Reset FFB", "Reset Force Feedback");
		key("Poste", "Matériel", "Vr.Recenter", "Recentrer la VR", "VR : Re-Center head position");
		return list;
	}

	public static string LegacyRole(string command)
	{
		return "FMT - " + LegacyLabel(command).Replace(".", " - ");
	}

	public static string LegacyLabel(string name)
	{
		string[] parts = name.Split('.');
		Dictionary<string, string> labels = new Dictionary<string, string>
		{
			{ "Refuel", "Ravitaillement" },
			{ "Tyres", "Pneus" },
			{ "Repairs", "Reparations" },
			{ "Brakes", "Freins" },
			{ "Driver", "Pilote" },
			{ "Ratio", "Ratio carburant" },
			{ "StopGo", "Stop et Go" },
			{ "Serve", "Effectuer" },
			{ "Defer", "Ne pas effectuer" },
			{ "Same", "Meme compose neuf" },
			{ "Plus", "+0,01" },
			{ "Minus", "-0,01" },
			{ "FL", "Avant gauche" },
			{ "FR", "Avant droit" },
			{ "RL", "Arriere gauche" },
			{ "RR", "Arriere droit" },
			{ "Next", "Choix suivant" },
			{ "Previous", "Choix precedent" },
			{ "Plus1", "+1 cran" },
			{ "Minus1", "-1 cran" },
			{ "Plus5", "+5 crans" },
			{ "Minus5", "-5 crans" },
			{ "Plus10", "+10" },
			{ "Minus10", "-10" },
			{ "Maximum", "Maximum" },
			{ "Minimum", "Minimum" },
			{ "Soft", "Soft neufs" },
			{ "Medium", "Medium neufs" },
			{ "Hard", "Hard neufs" },
			{ "Wet", "Pluie neufs" },
			{ "Bodywork", "Carrosserie" },
			{ "Replace", "Remplacer" },
			{ "Keep", "Conserver" }
		};
		return string.Join(".", parts.Select(delegate(string p)
		{
			if (!(p == "All"))
			{
				if (!(p == "None"))
				{
					if (!labels.ContainsKey(p))
					{
						return p;
					}
					return labels[p];
				}
				if (!(parts[0] == "Tyres"))
				{
					return "Ne pas reparer";
				}
				return "Aucun changement";
			}
			return (!(parts[0] == "Tyres")) ? "Tout reparer" : "Quatre roues";
		}));
	}
}
