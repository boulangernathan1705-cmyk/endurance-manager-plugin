# Inventaire des données Le Mans Ultimate

Relevé le 18/10/2026 sur le PC de Nathan, LMU en séance d'essais au Mans (Peugeot 9X8 2026), voiture au garage. Lecture seule : uniquement des GET sur l'API locale (http://localhost:6397) et une lecture de la mémoire partagée LMU_Data.

Légende : « utilisé » = déjà lu par notre plugin · « nouveau » = pas encore lu.

> Complété par un relevé en piste à 15 h 32 (sortie des stands, limiteur), voir la partie 4.

## 1. L'API REST (82 adresses GET dans le schéma)

48 adresses appelées sans paramètre, 45 répondent.

### Adresses qui répondent (au garage, en séance)
- **garage/UIScreen/RepairAndRefuel** (14 Ko) : écran du stand. Météo actuelle, carburant et énergie, menu du stand complet, temps de service, usure, prévision météo, heure.
- **garage/UIScreen/PitCarReview** (22 Ko) : même base + classement en direct (standings.vehiclesInOrder) et raceProgress.
- **garage/UIScreen/TireManagement** (16 Ko) : usure prévue par gomme, conso prévue, température idéale par gomme, stock de pneus.
- **garage/UIScreen/DriverHandOffStintEnd** (1,6 Ko) : fin de relais. scoringInfo (moyenne du relais, meilleur tour du pilote, pénalités en attente, arrêts faits) et roues (pression, températures, gomme).
- **garage/UIScreen/DriverHandOffStintStart** (0,6 Ko) : rapport du dernier relais (moyenne, meilleur tour, positions avant/après, durée).
- **garage/UIScreen/CarSetupOverview** (44 Ko) : réglages complets de la voiture + préréglages.
- **garage/UIScreen/SessionSetup** (4 Ko) : paramètres de la séance.
- **garage/getPlayerGarageData** (42 Ko) : les 172 réglages VM_* (valeur, texte, min/max, valeur enregistrée).
- **garage/summary** (26 Ko) : réglage chargé (activeSetup), résumé par catégorie, dossier du circuit. Son champ « car » montrait une autre voiture : à ne pas utiliser pour savoir quelle voiture roule.
- **garage/setup** (67 Ko) : liste des 382 fichiers de réglage.
- **garage/getVehicleCondition** (0,2 Ko) : état des freins, des pneus et de la suspension par roue, dégâts, carburant.
- **garage/tireinfo** (0,7 Ko) : par roue, températures gauche/centre/droite (K), pression, charge.
- **garage/brakeinfo** (22 o) : 4 valeurs, à 0 au garage. À vérifier en piste.
- **garage/PitMenu/receivePitMenu** (12 Ko) : le menu du stand seul (18 options).
- **strategy/pitstop-estimate** (0,1 Ko) : durée estimée de l'arrêt programmé, détaillée.
- **strategy/usage** (2,5 Ko) : pour chaque pilote de la séance, énergie restante par tour, relais, passage au stand.
- **watch/sessionInfo** (0,9 Ko) : météo, humidité de piste, temps écoulé/restant, phase.
- **watch/standings** (28 Ko) : toutes les voitures en direct (positions, écarts, secteurs, carburant, énergie, stand, pénalités, drapeaux, coordonnées).
- **watch/standings/history** (10 Ko) : historique tour par tour de chaque voiture.
- **watch/trackmap** (254 Ko) : le tracé du circuit, 3 261 points x/y/z avec un type par point.
- **sessions** (9 Ko) : réglages de la séance (heures de départ, échelle de temps, météo, carburant, dégâts…).
- **sessions/weather** (5,5 Ko) : météo programmée par séance (5 points : début, 25/50/75 %, fin).
- **sessions/GetGameState** (0,5 Ko) : heure du jour, phase, stand, nœud météo le plus proche.
- sessions/GetSessionsInfoForEvent · sessions/amount · sessions/opponents (584 adversaires IA)
- race/track (39 circuits avec leur longueur) · race/car et sessions/getAllVehicles (toutes les voitures)
- multiplayer/teams · options/liveInputs (volant et pédales)
- hud, replay/isActive, watch/replays, getLanguage, eacActive… : rien d'utile pour nous.

Ne répondent pas : CoopOverview (404, hors coop) et strategy/overall (400, attend un paramètre). Pendant le chargement d'une séance, presque tout répond 503 ou refuse la connexion.

Exclues volontairement : celles qui agissent (resetVRView, watch/play, multiplayer/join…), celles qui exigent un identifiant, et le jeton de connexion du profil.

### Par thème

**Pneus**
- RepairAndRefuel.wearables.tires[4] (1 = neuf) et getVehicleCondition.tireCondition[4] : nouveau
- TireManagement.expectedUsage.compoundsWearPerLap[] {type, wearPerLap} : nouveau
- TireManagement.optimalCompoundConditions.compounds[] {type, optimalTemperature} : utilisé
- TireManagement.tireInventory {newTires, maxAvailableTires, bestConditionsUsed} et tireInvGarageOptions (jeux disponibles et usure de chacun) : nouveau
- tireinfo {leftTemperature, centerTemperature, rightTemperature (K), pressure, load} par roue : nouveau
- DriverHandOffStintEnd.wheelInfo.wheelLocs[4] {tirePressure, tireTemp, brakeTemp, compound} : nouveau

**Énergie / carburant**
- fuelInfo {currentFuel, maxFuel, currentVirtualEnergy, maxVirtualEnergy, currentBattery, maxBattery} : nouveau
- TireManagement.expectedUsage {fuelConsumption, virtualEnergyFractionPerLap, fuelFractionPerLap} : utilisé
- strategy/usage : énergie restante tour par tour pour chaque pilote : nouveau
- watch/standings[].fuelFraction et veFraction (toutes les voitures) : nouveau

**Stand**
- pitStopTimes.times (36 temps) : utilisé
- pitMenu.pitMenu[18] : DAMAGE, DRIVER, VIRTUAL ENERGY 0–100 %, FUEL RATIO, TIRES, FL/FR/RL/RR TIRE, R WING, GRILLE, FL/FR/RL/RR PRESS 131–166 kPa, F/R BRAKE DUCT, REPLACE BRAKES : nouveau
- pitStopLength.timeInSeconds et strategy/pitstop-estimate {brakeDucts, brakes, damage, driverSwap, fuel, penalties, tires, total, ve} : nouveau
- pitRecommendations : nouveau

**Météo / heure**
- currentWeather {lightLevel, ambientTempKelvin, trackTempKelvin, cloudCoverage, humidity, rainIntensity, raining, airPressure} : lightLevel utilisé, le reste nouveau
- sessionTime.timeOfDay et GetGameState.timeOfDay : secondes depuis minuit : nouveau
- weatherForecast.nodes et sessions/weather (prévision par séance) : nouveau
- sessions : heures de départ, échelle de temps, météo : nouveau

**Setup**
- garage/summary.activeSetup : utilisé (nom du réglage)
- getPlayerGarageData : 172 réglages (aileron, radiateurs, carrossage, pincement, hauteurs, pressions de départ, ressorts, amortisseurs, anti-roulis, différentiel, répartition et migration de freinage, cartographie moteur, TC, régénération, rapports, carburant) : nouveau
- garage/setup (liste des réglages enregistrés) : nouveau

**Chronos / secteurs**
- watch/standings[] {bestLapTime, bestSectorTime1/2, lastLapTime, lastSectorTime1/2, currentSectorTime1/2, timeIntoLap, estimatedLapTime} : nouveau
- DriverHandOffStintEnd.scoringInfo {avgLapTime, avgLapTimeThisStint, bestLapTime, bestLapTimeOfDriver, completedLapsThisStint} : nouveau
- DriverHandOffStintStart.latestStintReport : nouveau

**Dégâts**
- wearables {body.aero, body.detachableParts[18], brakes[4], suspension[4]} et getVehicleCondition.vehicleDamage : nouveau

**Pénalités / drapeaux**
- watch/standings[] {penalties, flag, underYellow, countLapFlag, finishStatus} et scoringInfo.NumOfOutstandingPenalties : nouveau

**Autres voitures**
- watch/standings (positions x/y/z, vitesse, écarts, stand, carburant, énergie), PitCarReview.standings.vehiclesInOrder (gommes de chacun) et standings/history : nouveau

**Circuit**
- watch/trackmap : 3 261 points, de quoi dessiner la carte de l'onglet Analyse : nouveau
- race/track (longueur de chaque circuit) : nouveau

## 2. La mémoire partagée LMU_Data (324 820 octets)

Même structure que rFactor 2 : ScoringInfo à 1632, VehicleScoring à 2192 (584 octets par voiture, 104 voitures max), TelemInfo à 128468 (1 888 octets par voiture), roues à TelemInfo + 848 + i × 260 (AVG, AVD, ARG, ARD).

**ScoringInfo** : nom du circuit, séance, nombre de voitures, en piste (utilisés) · nuages, vent, humidité moyenne de la trajectoire (nouveaux) · pluie, températures air et piste, humidité mini/maxi (utilisés).

**VehicleScoring** (vaut aussi pour les adversaires) : nom, voiture, tours, derniers secteurs, au stand, catégorie (utilisés) · secteur en cours, distance dans le tour, position latérale, meilleurs secteurs et meilleur tour, arrêts, pénalités, place, écarts, position x/y/z, temps dans le tour, tour estimé, drapeaux (nouveaux).

**TelemInfo** (joueur) : temps, vitesse, carburant, dégâts, gommes, tour invalide, énergie virtuelle (utilisés) · position x/y/z, accélérations (G), rapport, régime, températures eau et huile, accélérateur/frein/volant bruts, hauteurs de caisse avant/arrière, appuis avant/arrière (nouveaux, à vérifier en piste).

**Roues** : température des freins, pression, usure, carcasse (utilisés) · débattement, hauteur de caisse, rotation (blocages), vitesses de glisse, carrossage, charge, adhérence, températures intérieur/milieu/extérieur, surface, crevaison, roue perdue, rayon, températures de la couche intérieure (nouveaux).

## 3. Ce qui sert à l'onglet « Analyse »
- **Mode expert par roue** : températures int/milieu/ext (écart), hauteur de caisse (moyenne et mini), pression (max), freins (max), pneu restant. Moteur : huile et eau (moyenne et max). Tout est dans la mémoire partagée, lu à 10 Hz.
- **Analyse du tour sur la carte** : la position donne la trace du pilote, watch/trackmap le contour du circuit. S'y ajoutent vitesse, accélérateur, frein, volant, rapport, glisse et rotation des roues (blocages).
- **Comparaison entre coéquipiers** : une trace par pilote ; la distance depuis la ligne permet d'aligner deux tours point par point.

## 4. Vérifié en piste

Trois relevés à 2 s d'écart à la sortie des stands (48 puis 60 km/h), lecture seule.

**Mémoire du jeu, voiture du joueur**
- Position x/z : 33 m parcourus en 2 s, soit 60 km/h. Cohérent avec la vitesse.
- Rapport 1, régime 3 171 → 3 752 tr/min : OK.
- Accélérateur / frein / volant : 0 à 1 pour les pédales, −1 à 1 pour le volant : OK.
- Eau 61,9 °C, huile 61,5 °C, déjà en °C : OK.
- **Hauteurs de caisse avant/arrière (mFrontRideHeight / mRearRideHeight) : toujours à 0, LMU ne les remplit pas.** La hauteur par roue (mRideHeight) marche : avant 3,8–4,6 cm, arrière 5,6–6,1 cm, cohérent avec le setup. Le plugin prend donc la moyenne des deux roues de chaque essieu.
- Rotation des roues −46,2 rad/s × rayon 0,36 m = 16,6 m/s, égal à la vitesse au sol : la détection des blocages marche.
- Températures de bande mTemperature[3] : ordre gauche / centre / droite. Sur une roue gauche l'extérieur est [0], sur une roue droite l'extérieur est [2].
- Carcasse 29 °C, pression 130,5 kPa, usure 1 → 0,9992 : OK.

**API en piste** : les 45 mêmes adresses répondent.
- getVehicleCondition est en direct et léger (0,2 Ko) : carburant, état des pneus, état des freins.
- GetGameState.MultiStintState passe à « DRIVING » en piste.
- brakeinfo donne l'épaisseur des disques en mètres (0,034 avant, 0,032 arrière, comme dans le setup).
- tireinfo ne bouge pas en roulant (valeurs du setup) : pour les températures, la mémoire du jeu reste la source.

**Ratio carburant / énergie (menu des stands)**
- FUEL RATIO va de 0.05 à 1.10 par crans de 0.01 : litres de carburant pour 1 % d'énergie virtuelle (0.86 × 100 % = 86 L, le jeu l'écrit dans FUEL_CAPACITY du setup).
- Même valeur dans le setup : VM_FUEL_LEVEL. L'énergie virtuelle est en joules dans fuelInfo (880 MJ = 100 %).
- Le menu des stands n'a de valeur utile qu'une fois réglé (il peut afficher 0 % / 0.05).
