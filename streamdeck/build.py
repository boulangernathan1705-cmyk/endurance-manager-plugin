# Profil Stream Deck « Endurance Manager · FMT » (Stream Deck MK.2, 15 touches), maquette validée par Nathan le
# 24/11/2026 : https://claude.ai/artifact/Gi7MJDFribekYZYFzec85g
#
# Fabrique « Endurance Manager FMT.streamDeckProfile » à côté de ce fichier : python3 streamdeck/build.py
# (dépendance : cairosvg pour dessiner les touches). Le fichier produit est commité : les pilotes le double-cliquent.
#
# Toutes les touches de réglage sont des boutons dynamiques de SimHub 9.13.6 (plugin Stream Deck « Simhub dynamic
# button », livré dans SimHub\_Addons\StreamDeckPlugins, Stream Deck 7.6 ou plus) : image SVG, valeur en direct
# (formule), image verte quand la condition est vraie, appui = rôle « FMT - … » ou « EM - Profil N » du Control Mapper
# (créés par le plugin, modules Black box sur boutons / Réglages voiture en course). Dossiers et retour : actions de
# Stream Deck.
#
# DA : celle du logo FMT (bandes arc-en-ciel) sur le fond du site ; une couleur par famille, sur l'icône, la bande du
# bas et le coin des dossiers : Voiture rouge, Stand orange, Pneus jaune, Poste vert, Caméra bleu.
import io
import base64
import json
import re
import os
import uuid
import zipfile

import cairosvg

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "Endurance Manager FMT.streamDeckProfile")

FAM = {"car": "#FF5A4E", "stand": "#FF9A2E", "tyre": "#FFD23F", "post": "#5CC46A", "cam": "#3CC4FF", "w": "#EEF3F2"}
ACTIVE = ("#6FE496", "#3CB866")

I = {
    "sec": "M12 8a4 4 0 1 0 0 8a4 4 0 1 0 0-8M12 2v2.5M12 19.5V22M2 12h2.5M19.5 12H22M4.9 4.9l1.8 1.8M17.3 17.3l1.8 1.8M4.9 19.1l1.8-1.8M17.3 6.7l1.8-1.8",
    "inter": "M8 5.5a3.5 3.5 0 0 1 6 1.5M8 2v1.2M3.5 6.5h1.2M4.6 3.6l.9.9M7 19h10a4 4 0 0 0 0-8a5.5 5.5 0 0 0-10.4 1.6A3.2 3.2 0 0 0 7 19z",
    "pluie": "M7 15h10a4 4 0 0 0 0-8a5.5 5.5 0 0 0-10.4 1.6A3.2 3.2 0 0 0 7 15zM8 18l-1 3M12 18l-1 3M16 18l-1 3",
    "stop": "M4 4h16v16H4zM9.5 17V7h3.5a3 3 0 0 1 0 6H9.5",
    "lim": "M4 17a8 8 0 1 1 16 0M12 17l4.5-5M7 17h.01M17 17h.01",
    "car": "M3 16v-3l2.5-4.5h9L19 13h2v3M2 16h20M5.5 16a2 2 0 1 0 4 0M14.5 16a2 2 0 1 0 4 0",
    "stand": "M14.7 6.3a4 4 0 0 0 5 5L12 19a2.1 2.1 0 0 1-3-3l7.7-7.7zM15 3.5a4 4 0 0 1 5.5 5.5",
    "tyre": "M3 12a9 9 0 1 0 18 0a9 9 0 1 0-18 0M8 12a4 4 0 1 0 8 0a4 4 0 1 0-8 0M12 3v3M12 18v3M3 12h3M18 12h3",
    "lights": "M11 5c-4 0-7 3-7 7s3 7 7 7zM14 7.5h7M14 12h7M14 16.5h7",
    "wiper": "M3 18a9 9 0 0 1 18 0M12 18L6.5 8.5",
    "cam": "M3 7.5h4l2-2.5h6l2 2.5h4V19H3zM12 10a3.2 3.2 0 1 0 0 6.4a3.2 3.2 0 1 0 0-6.4",
    "seat": "M8 3h4l-1.2 10H18v4H7zM9 21h8M5 9v8",
    "brake": "M4 12a8 8 0 1 0 16 0a8 8 0 1 0-16 0M10 12a2 2 0 1 0 4 0a2 2 0 1 0-4 0M12 6.5v.01M17.5 12h.01M12 17.5v.01M6.5 12h.01",
    "repair": "M12 3l8 3v6c0 5-3.5 8-8 9c-4.5-1-8-4-8-9V6zM12 9v6M9 12h6",
    "flash": "M13 2L5 14h6l-1 8l8-12h-6z",
    "energy": "M3 7.5h15.5v9H3zM21 10.5v3M11.5 9l-2.5 3h4l-2.5 3",
    "fuel": "M4 21V5h9v16M3 21h11M13 9h3l2 2v6a1.5 1.5 0 0 0 3 0V8l-3-3M6.5 8.5h4",
    "press": "M12 4a8 8 0 1 0 0 16a8 8 0 1 0 0-16M12 12l3.5-3.5M8 15.5h8",
    "aero": "M2.5 8.5h19l-2 3.5h-15zM8 12v6M16 12v6M6 18h4M14 18h4",
    "driver": "M4 15a8 8 0 0 1 16-2.5V17H8.5A4.5 4.5 0 0 1 4 15zM12 10.5h8",
    "more": "M5 12h.01M12 12h.01M19 12h.01",
    "up": "M6 15l6-6 6 6",
    "down": "M6 9l6 6 6-6",
    "four": "M5 4h4v7H5zM15 4h4v7h-4zM5 13h4v7H5zM15 13h4v7h-4z",
    "back": "M10 6l-6 6 6 6M4 12h10a5 5 0 0 1 5 5v1",
    "mirror": "M3 8h18v8H3zM12 16v4M8 20h8",
    "replay": "M4 12a8 8 0 1 0 2.3-5.7M4 4v4h4M10 9l5 3-5 3z",
    "vr": "M3 8h18v8h-6l-3-3-3 3H3z",
}


def esc(t):
    return t.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def svg_key(icon=None, label="", value="", fam="w", kind="", live=False, big=False):
    """One key, 144 × 144. live: the bottom third stays free for the live title Stream Deck draws over the image."""
    color = FAM.get(fam, fam)
    bg_top, bg_bot, border = "#1C2427", "#10171A", "#2B3236"
    text, muted = "#FFFFFF", "#A9B4B8"
    parts = []
    if kind == "active":
        bg_top, bg_bot, border = ACTIVE[0], ACTIVE[1], "#8CF0AD"
        color, text, muted = "#062312", "#04170B", "#0A3A1D"
    if kind == "pm":
        bg_top, bg_bot = "#151C1F", "#0B1012"
    if kind == "back":
        bg_top, bg_bot = "#273034", "#171E21"
    if kind == "empty":
        return ('<svg xmlns="http://www.w3.org/2000/svg" width="144" height="144" viewBox="0 0 144 144"><rect width="144" height="144" fill="#0A0D0F"/></svg>')
    parts.append(f'<defs><linearGradient id="g" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{bg_top}"/>'
                 f'<stop offset="1" stop-color="{bg_bot}"/></linearGradient></defs>')
    parts.append(f'<rect width="144" height="144" fill="url(#g)"/><rect x="1" y="1" width="142" height="142" fill="none" stroke="{border}" stroke-width="2"/>')
    if kind == "folder":
        parts.append(f'<path d="M0 0h34L0 34z" fill="{color}"/>')
        parts.append(f'<path d="M122 120l8 8-8 8" fill="none" stroke="{color}" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"/>')
    if kind not in ("pm", "back", "active"):
        parts.append(f'<rect x="0" y="136" width="144" height="8" fill="{color}"/>')
    # Layout: icon, label, value (static), or icon + label with the live title below.
    if kind == "pm":
        parts.append(f'<g transform="translate(32 32) scale(3.33)"><path d="{I[icon]}" fill="none" stroke="{color}" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"/></g>')
    else:
        size = 58 if not (live or value) else 46
        if big and not icon:
            size = 0
        y_icon = 14 if (live or value) else 26
        if icon and size:
            k = size / 24
            parts.append(f'<g transform="translate({72 - size / 2} {y_icon}) scale({k})"><path d="{I[icon]}" fill="none" stroke="{color if kind != "back" else "#EEF3F2"}" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></g>')
        y_label = (y_icon + size + 18) if icon and size else 34
        if label:
            parts.append(f'<text x="72" y="{y_label}" font-family="Arial, Liberation Sans, FreeSans" font-weight="bold" font-size="{16 if len(label) <= 11 else 13}" fill="{muted}" text-anchor="middle" letter-spacing="{1 if len(label) <= 11 else 0}">{esc(label.upper())}</text>')
        if value and not live:
            lines = value.split("\n")
            fs = 22 if max(len(l) for l in lines) <= 10 else 18
            y = y_label + 28
            for line in lines:
                parts.append(f'<text x="72" y="{y}" font-family="Arial, Liberation Sans, FreeSans" font-weight="bold" font-size="{fs}" fill="{text}" text-anchor="middle">{esc(line)}</text>')
                y += fs + 4
    return '<svg xmlns="http://www.w3.org/2000/svg" width="144" height="144" viewBox="0 0 144 144">' + "".join(parts) + "</svg>"


class Profile:
    def __init__(self):
        self.pages = {}
        self.images = {}
        self.preview = {}

    def image(self, svg):
        name = "Images/" + str(uuid.uuid5(uuid.NAMESPACE_URL, svg)).upper() + ".png"
        if name not in self.images:
            self.images[name] = cairosvg.svg2png(bytestring=svg.encode("utf-8"), output_width=144, output_height=144)
        return name


def state(image, title="", show=False, size=16):
    return {"Image": image, "FontSize": size, "FontStyle": "Bold", "TitleAlignment": "bottom", "TitleColor": "#FFFFFF",
            "ShowTitle": show, "FontUnderline": False, "Title": title, "FontFamily": "Verdana"}


def pid(name):
    return str(uuid.uuid5(uuid.NAMESPACE_URL, "em-streamdeck/" + name))


# Key kinds, each returning a Stream Deck action (needs the profile for its images).
# SimHub dynamic buttons (Stream Deck plugin « Simhub dynamic button », shipped with SimHub 9.13.6 in
# _Addons/StreamDeckPlugins, needs Stream Deck 7.6). The whole button (states, images, live text, action) is kept in
# the Stream Deck action's settings, in the format SimHub exports (.shsdb), read from SimHub's templates and from a
# button made by Nathan on 24/11/2026 (press = Control Mapper role: ActionMode 3, ControlMapperRoleName).
def b64svg(svg):
    return base64.b64encode(svg.encode("utf-8")).decode("ascii")


def icon_style(svg, font_size=14):
    return {"ReverseIconColors": False, "IconMarginLeft": 0, "IconMarginTop": 0, "IconMarginRight": 0, "IconMarginBottom": 0,
            "TextMarginBottom": 10, "LabelVerticalAlignment": 2, "Image": b64svg(svg), "UseBlinkingBorderColor": False,
            "UseBlinkingIconBackgroundColor": False, "UseBlinkingIconForegroundColor": False, "UseBlinkingTextColor": False,
            "BlinkingDuration": 500, "BlinkingTwoPhases": False, "BlinkingOffDuration": 500, "BlinkingIconBackgroundColor": "#FF000000",
            "BlinkingIconForegroundColor": "#00FFFFFF", "BlinkingTextColor": "#FFFF0000", "Bold": True, "Italic": False,
            "BorderColor": "#FF000000", "BlinkingBorderColor": "#FFFF0000", "BorderThickness": 0, "EnableBlinking": False,
            "FontSize": font_size, "IconBackgroundColor": "#FF000000", "IconForegroundColor": "#00FFFFFF", "LabelFont": "Arial",
            "TextColor": "#FFFFFFFF", "IconAlpha": 100.0}


def sid(*parts):
    return str(uuid.uuid5(uuid.NAMESPACE_URL, "em-streamdeck-dyn/" + "/".join(parts)))


def dynamic(key, off_svg, r="", formula="", on_svg=None, on_when="", size=150):
    """One dynamic button: base image, optional live text at the bottom (NCalc formula), optional second image while
    on_when is true, press = Control Mapper role r."""
    def make(p):
        first = {"actionMode": 0, "labelMode": 3 if formula else 0, "iconMode": 0,
                 "OnPress": {"ActionMode": 3, "ControlMapperRoleName": r} if r else {}, "OnShortPress": {}, "OnLongPress": {},
                 "HasActionOverrides": False, "ButtonRenderer": {"IconStyle": icon_style(off_svg)}, "StaticLabel": "",
                 "Uid": sid(key, "0"), "StateTitle": "Button state", "DisplayStateTitle": "Default state",
                 "IsEnabledExpression": {"Expression": ""}, "IsConditional": False}
        if formula:
            first["RichLabel"] = [{"Formula": {"Expression": formula}, "Format": 0, "Size": float(size)}]
        states = [first]
        if on_svg and on_when:
            states.append({"actionMode": 0, "labelMode": 2, "iconMode": 0, "HasActionOverrides": False,
                           "ButtonRenderer": {"IconStyle": {"Overrides": ["Image", "TextColor"], "Image": b64svg(on_svg), "TextColor": "#FF04170B"}},
                           "Uid": sid(key, "1"), "StateTitle": "Button state", "DisplayStateTitle": "Button state",
                           "IsEnabledExpression": {"Expression": on_when}, "IsConditional": True})
        button = {"StreamDeckButtonSetting": {"FromModel": None, "DesignWidth": 144, "DesignHeight": 144,
                                              "SettingsBuilder": {"Settings": [], "IsEditMode": False, "Prefix": None},
                                              "StateToggleMode": 1 if len(states) > 1 else 0, "Target": 1, "Uid": sid(key, "setting"),
                                              "FormatVersion": 1, "States": states},
                  "Uid": sid(key, "instance"), "ModelId": None, "InstanceMode": 0, "SettingsValues": {}, "SettingsValuesOf": ""}
        return {"Plugin": {"UUID": "com.wotever.simhubdynamicbutton", "Name": "Simhub dynamic button", "Version": "1.6"},
                "Resources": None, "Settings": button, "State": 0, "Name": key,
                # Shown by Stream Deck until SimHub draws the button.
                "States": [state(p.image(off_svg))], "LinkedTitle": False,
                "UUID": "com.wotever.simhubdynamicbutton", "ActionID": sid(key, "action"),
                "EMPreview": {"formula": formula, "on_when": on_when, "on": p.image(on_svg) if on_svg and on_when else None}}
    return make


def role(r, icon=None, label="", value="", fam="w", kind=""):
    return dynamic(r + label + value, svg_key(icon, label, value, fam, kind), r=r)


def live(title_prop, icon=None, label="", fam="w", r="", state_prop="", fmt="", active_when_on=False, kind="", size=18):
    """A key showing a SimHub property live at the bottom; its press runs role r (if any); green while state_prop is on."""
    formula = ""
    if title_prop and title_prop == state_prop:
        # On/off (limiteur, phares, essuie-glace) : la touche passe au vert, sans texte.
        title_prop, active_when_on = "", True
    if title_prop:
        formula = f"format([{title_prop}], '0')" if fmt == ":0" else f"[{title_prop}]"
    off = svg_key(icon, label, "", fam, kind, live=True)
    on = svg_key(icon, label, "", fam, "active", live=True) if active_when_on and state_prop else None
    return dynamic(label + r + (state_prop or title_prop), off, r=r, formula=formula, on_svg=on, on_when=f"[{state_prop}]" if on else "",
                   size=150 if size >= 18 else 120)


def folder(target, icon=None, label="", value="", fam="w"):
    def make(p):
        return {"Plugin": {"UUID": "com.elgato.streamdeck.profile.openchild", "Name": "Create Folder", "Version": "1.0"},
                "Resources": None, "Settings": {"ProfileUUID": pid(target)}, "State": 0, "Name": label,
                "States": [state(p.image(svg_key(icon, label, value, fam, "folder")))], "LinkedTitle": False,
                "UUID": "com.elgato.streamdeck.profile.openchild", "ActionID": str(uuid.uuid5(uuid.NAMESPACE_URL, "folder" + target))}
    return make


def back():
    def make(p):
        return {"Plugin": {"UUID": "com.elgato.streamdeck.profile.backtoparent", "Name": "Open Parent Folder", "Version": "1.0"},
                "Resources": None, "Settings": {}, "State": 0, "Name": "Retour",
                "States": [state(p.image(svg_key("back", "", "Retour", "w", "back")))], "LinkedTitle": False,
                "UUID": "com.elgato.streamdeck.profile.backtoparent", "ActionID": "retour"}
    return make


def plus(r, fam):
    return role(r, "up", "", "", fam, "pm")


def minus(r, fam):
    return role(r, "down", "", "", fam, "pm")


EM = "EnduranceManagerPlugin."
FMT = "FMTCompanionPlugin."
GD = "DataCorePlugin.GameData."
V = "FMT - Voiture - "
S = "FMT - Stand - "
C = "FMT - Caméra - "
P = "FMT - Poste - "


def profile_key(n, icon):
    # Title = the profile's name (renamable), « conseillé » under it when the track asks for it; green when active.
    return live(EM + f"Profils.{n}.Titre", icon, "Profil", "w", r=f"EM - Profil {n}", state_prop=EM + f"Profils.{n}.Actif", active_when_on=True)


def tag(text, fam):
    """A key that shows only a label (a value LMU does not give SimHub)."""
    return live("", None, text, fam)


PAGES = {
    # Positions « colonne,ligne » (5 × 3).
    "Accueil": {
        "0,0": profile_key(1, "sec"), "1,0": profile_key(2, "inter"), "2,0": profile_key(3, "pluie"),
        "3,0": role(V + "Demande d'arrêt", "stop", "Demande", "Arrêt", "car"),
        "4,0": live(GD + "PitLimiterOn", "lim", "Limiteur", "car", r=V + "Limiteur", state_prop=GD + "PitLimiterOn"),
        "0,1": folder("Voiture", "car", "Voiture", "TC, ABS, carto", "car"),
        "1,1": folder("Stand", "stand", "Stand", "Énergie, ratio", "stand"),
        "2,1": folder("Pneus", "tyre", "Pneus", "4 roues", "tyre"),
        "3,1": live(GD + "HeadlightsActive", "lights", "Phares", "car", r=V + "Phares", state_prop=GD + "HeadlightsActive"),
        "4,1": live(GD + "WipersActive", "wiper", "Essuie-glace", "car", r=V + "Essuie-glace", state_prop=GD + "WipersActive"),
        "0,2": folder("Caméra", "cam", "Caméra", "TV, replay", "cam"),
        "1,2": folder("Poste", "seat", "Poste", "Siège, rétros", "post"),
        "2,2": live(FMT + "Pit.Brakes.Short", "brake", "Freins", "stand", r=S + "Freins changer ou garder", state_prop=FMT + "Pit.Brakes.Replace"),
        "3,2": live(FMT + "Pit.Damage.Short", "repair", "Réparations", "stand", r=S + "Réparations choix suivant en boucle"),
        "4,2": role(V + "Appel de phares", "flash", "Appel", "de phares", "car"),
    },
    "Voiture": {
        "0,0": back(), "1,0": plus(V + "TC suivant", "car"), "2,0": plus(V + "Coupure TC suivant", "car"),
        "3,0": plus(V + "ABS suivant", "car"), "4,0": plus(V + "Carto moteur suivante", "car"),
        "0,1": folder("Voiture suite", "more", "Suite", "Glisse, régén…", "car"),
        "1,1": live(EM + "Voiture.Reglages.TC", None, "TC", "car", size=26), "2,1": live(EM + "Voiture.Reglages.CoupureTC", None, "Coupure TC", "car", size=26),
        "3,1": live(EM + "Voiture.Reglages.ABS", None, "ABS", "car", size=26), "4,1": live(EM + "Voiture.Reglages.Carto", None, "Carto", "car", size=26),
        "0,2": live(EM + "Profils.Actif", "sec", "Profil", "w"),
        "1,2": minus(V + "TC précédent", "car"), "2,2": minus(V + "Coupure TC précédent", "car"),
        "3,2": minus(V + "ABS précédent", "car"), "4,2": minus(V + "Carto moteur précédente", "car"),
    },
    "Voiture suite": {
        "0,0": back(), "1,0": plus(V + "TC glisse suivant", "car"), "2,0": plus(V + "Régénération suivante", "car"),
        "3,0": plus(V + "Répartition vers l'avant", "car"), "4,0": plus(V + "Migration vers l'avant", "car"),
        "0,1": folder("Antiroulis", "car", "Antiroulis", "Avant, arrière", "car"),
        "1,1": live(EM + "Voiture.Reglages.GlisseTC", None, "TC glisse", "car", size=26), "2,1": live(EM + "Voiture.Reglages.Regen", None, "Régén", "car", size=26),
        "3,1": live(EM + "Voiture.Reglages.Repartition", None, "Répartition", "car", size=22), "4,1": live(EM + "Voiture.Reglages.Migration", None, "Migration", "car", size=26),
        "1,2": minus(V + "TC glisse précédent", "car"), "2,2": minus(V + "Régénération précédente", "car"),
        "3,2": minus(V + "Répartition vers l'arrière", "car"), "4,2": minus(V + "Migration vers l'arrière", "car"),
    },
    "Antiroulis": {
        "0,0": back(), "1,0": plus(V + "Antiroulis avant suivant", "car"), "2,0": plus(V + "Antiroulis arrière suivant", "car"),
        "1,1": live(EM + "Voiture.Reglages.AntiroulisAV", None, "Antiroulis AV", "car", size=26), "2,1": live(EM + "Voiture.Reglages.AntiroulisAR", None, "Antiroulis AR", "car", size=26),
        "1,2": minus(V + "Antiroulis avant précédent", "car"), "2,2": minus(V + "Antiroulis arrière précédent", "car"),
    },
    "Stand": {
        "0,0": back(), "1,0": plus(S + "Énergie suivante", "stand"), "2,0": plus(S + "Ratio suivant", "stand"),
        "3,0": plus(S + "Pressions 4 roues suivantes", "stand"),
        "4,0": live(FMT + "Pit.Brakes.Short", "brake", "Freins", "stand", r=S + "Freins changer ou garder", state_prop=FMT + "Pit.Brakes.Replace"),
        "0,1": folder("Pneus", "tyre", "Pneus", "", "tyre"),
        "1,1": live(FMT + "Pit.VE.Percent", "energy", "Énergie", "stand", fmt=":0"),
        "2,1": live(FMT + "Pit.Ratio", "fuel", "Ratio carbu", "stand"),
        "3,1": live(FMT + "Pit.FLPRESS", "press", "Pressions", "stand"),
        "4,1": live(FMT + "Pit.Damage.Short", "repair", "Réparations", "stand", r=S + "Réparations choix suivant en boucle"),
        "0,2": folder("Aéro", "aero", "Aéro", "Aileron, écopes", "stand"),
        "1,2": minus(S + "Énergie précédente", "stand"), "2,2": minus(S + "Ratio précédent", "stand"),
        "3,2": minus(S + "Pressions 4 roues précédentes", "stand"),
        "4,2": live(FMT + "Pit.Driver.Short", "driver", "Pilote", "stand", r=S + "Pilote suivant en boucle"),
    },
    "Pneus": {
        "0,0": back(),
        "1,0": live(FMT + "Pit.Tyre.FL.Badge", "tyre", "Avant gauche", "tyre", r=S + "Pneu avant gauche gomme suivante en boucle"),
        "2,0": live(FMT + "Pit.Tyre.FR.Badge", "tyre", "Avant droit", "tyre", r=S + "Pneu avant droit gomme suivante en boucle"),
        "1,1": live(FMT + "Pit.Tyre.RL.Badge", "tyre", "Arrière gauche", "tyre", r=S + "Pneu arrière gauche gomme suivante en boucle"),
        "2,1": live(FMT + "Pit.Tyre.RR.Badge", "tyre", "Arrière droit", "tyre", r=S + "Pneu arrière droit gomme suivante en boucle"),
        "4,0": role(S + "Pneus 4 roues même gomme neuve", "four", "4 roues", "Neufs même\ngomme", "tyre"),
        "4,1": live(FMT + "Pit.Tyres.Badge", "four", "4 roues", "tyre", r=S + "Pneus 4 roues gomme suivante en boucle"),
        "4,2": role(S + "Pneus 4 roues aucun changement", "four", "4 roues", "Aucun\nchangement", "tyre"),
    },
    "Aéro": {
        "0,0": back(), "1,0": plus(S + "Aileron suivant", "stand"),
        "1,1": live(FMT + "Pit.RWING", "aero", "Aileron", "stand"), "1,2": minus(S + "Aileron précédent", "stand"),
        "3,0": live(FMT + "Pit.GRILLE", "aero", "Calandre", "stand", r=S + "Calandre suivante en boucle"),
        "3,1": live(FMT + "Pit.FBRAKEDUCT", "brake", "Écopes AV", "stand", r=S + "Écopes avant suivantes en boucle"),
        "3,2": live(FMT + "Pit.RBRAKEDUCT", "brake", "Écopes AR", "stand", r=S + "Écopes arrière suivantes en boucle"),
    },
    "Caméra": {
        "0,0": back(), "1,0": role(C + "Conduite", "cam", "Conduite", "", "cam"), "2,0": role(C + "Embarquée", "cam", "Embarquée", "", "cam"),
        "3,0": role(C + "Poursuite", "cam", "Poursuite", "", "cam"), "4,0": role(C + "Spectateur", "cam", "Spectateur", "", "cam"),
        "0,1": role(C + "Swingman", "cam", "Swingman", "", "cam"), "1,1": role(C + "Voiture précédente", "up", "Voiture", "précédente", "cam"),
        "2,1": role(C + "Revenir à ma voiture", "car", "Ma voiture", "", "cam"), "3,1": role(C + "Voiture suivante", "down", "Voiture", "suivante", "cam"),
        "4,1": role(C + "Changer de vue", "cam", "Changer", "de vue", "cam"),
        "0,2": role(C + "Zoom TV -", "down", "Zoom TV", "−", "cam"), "1,2": role(C + "Zoom TV +", "up", "Zoom TV", "+", "cam"),
        "2,2": folder("Swingman", "cam", "Swing", "", "cam"), "3,2": folder("Caméra libre", "cam", "Libre", "", "cam"),
        "4,2": folder("Replay", "replay", "Replay", "", "cam"),
    },
    "Swingman": {
        "0,0": back(), "2,0": role(C + "Swingman haut", "up", "Haut", "", "cam"), "2,2": role(C + "Swingman bas", "down", "Bas", "", "cam"),
        "1,1": role(C + "Swingman gauche", "back", "Gauche", "", "cam"), "3,1": role(C + "Swingman droite", None, "Droite", "›", "cam"),
        "1,0": role(C + "Swingman zoom -", None, "Zoom", "−", "cam"), "3,0": role(C + "Swingman zoom +", None, "Zoom", "+", "cam"),
        "4,0": role(C + "Swingman reset", None, "Reset", "", "cam"),
        "0,2": role(C + "Regard à gauche", None, "Regard", "gauche", "cam"), "3,2": role(C + "Regard arrière", None, "Regard", "arrière", "cam"),
        "4,2": role(C + "Regard à droite", None, "Regard", "droite", "cam"),
    },
    "Caméra libre": {
        "0,0": back(), "1,0": role(C + "Caméra libre tourner à gauche", None, "Tourner", "gauche", "cam"),
        "3,0": role(C + "Caméra libre tourner à droite", None, "Tourner", "droite", "cam"),
        "0,1": role(C + "Caméra libre monter", "up", "Monter", "", "cam"), "0,2": role(C + "Caméra libre descendre", "down", "Descendre", "", "cam"),
        "1,1": role(C + "Caméra libre à gauche", None, "Gauche", "", "cam"), "2,0": role(C + "Caméra libre avancer", "up", "Avancer", "", "cam"),
        "2,1": role(C + "Caméra libre on/off", "cam", "Libre", "on/off", "cam"), "2,2": role(C + "Caméra libre reculer", "down", "Reculer", "", "cam"),
        "3,1": role(C + "Caméra libre à droite", None, "Droite", "", "cam"),
        "4,0": role(C + "Caméra libre incliner vers le haut", None, "Incliner", "haut", "cam"),
        "4,1": role(C + "Caméra libre incliner vers le bas", None, "Incliner", "bas", "cam"),
        "1,2": role(C + "Caméra libre recentrer", None, "Recentrer", "", "cam"), "3,2": role(C + "Caméra libre lente", None, "Lente", "", "cam"),
        "4,2": folder("Objectif", "cam", "Objectif", "", "cam"),
    },
    "Objectif": {
        "0,0": back(), "1,0": role(C + "Objectif libre on/off", "cam", "Objectif", "on/off", "cam"),
        "0,1": role(C + "Mise au point -", None, "Mise au point", "−", "cam"), "1,1": role(C + "Mise au point +", None, "Mise au point", "+", "cam"),
        "0,2": role(C + "Exposition -", None, "Exposition", "−", "cam"), "1,2": role(C + "Exposition +", None, "Exposition", "+", "cam"),
        "2,0": role(C + "Mise au point sur la voiture", "car", "Point", "voiture", "cam"),
        "3,1": role(C + "Ouverture -", None, "Ouverture", "−", "cam"), "4,1": role(C + "Ouverture +", None, "Ouverture", "+", "cam"),
        "3,0": role(C + "Caméra libre pencher à gauche", None, "Pencher", "gauche", "cam"),
        "4,0": role(C + "Caméra libre pencher à droite", None, "Pencher", "droite", "cam"),
        "4,2": role(C + "Caméra libre à la souris", None, "Souris", "", "cam"),
    },
    "Replay": {
        "0,0": back(), "1,0": role(C + "Replay lecture", "replay", "Lecture", "", "cam"),
        "1,1": role(C + "Replay lecture arrière", None, "Lecture", "arrière", "cam"), "2,0": role(C + "Replay stop", None, "Stop", "", "cam"),
        "0,1": role(C + "Replay retour rapide", None, "Retour", "rapide", "cam"), "2,1": role(C + "Replay avance rapide", None, "Avance", "rapide", "cam"),
        "3,0": role(C + "Replay ralenti", None, "Ralenti", "", "cam"), "4,0": role(C + "Ralenti instantané", None, "Ralenti", "instantané", "cam"),
        "0,2": role(C + "Garder la caméra TV", None, "Garder", "caméra TV", "cam"), "1,2": role(C + "Pause du suivi TV", None, "Pause", "suivi TV", "cam"),
        "2,2": role(C + "Zoom TV reset", None, "Zoom TV", "reset", "cam"), "3,2": role(C + "Noms des voitures", None, "Noms", "voitures", "cam"),
    },
    "Poste": {
        "0,0": back(), "0,2": role(P + "Rétroviseur", "mirror", "Rétroviseur", "", "post"),
        "1,0": role(P + "FOV vertical -", None, "FOV", "−", "post"), "3,0": role(P + "FOV vertical +", None, "FOV", "+", "post"),
        "1,1": role(P + "Siège avant", "seat", "Siège", "avant", "post"), "3,1": role(P + "Siège arrière", "seat", "Siège", "arrière", "post"),
        "2,0": role(P + "Siège haut", "up", "Siège", "haut", "post"), "2,2": role(P + "Siège bas", "down", "Siège", "bas", "post"),
        "4,0": role(P + "Recentrer la VR", "vr", "Recentrer", "la VR", "post"), "4,2": role(P + "Reset FFB", None, "Reset", "FFB", "post"),
        "4,1": folder("Rétros", "mirror", "Rétros", "", "post"),
    },
    "Rétros": {
        "0,0": back(), "1,0": folder("Rétro gauche", "mirror", "Gauche", "", "post"), "2,0": folder("Rétro droit", "mirror", "Droit", "", "post"),
        "3,0": folder("Rétro central", "mirror", "Central", "", "post"), "4,0": folder("Rétros tous", "mirror", "Tous", "", "post"),
        "1,1": folder("Largeur", "mirror", "Largeur", "", "post"), "2,1": folder("Angle central", "mirror", "Angle", "central", "post"),
    },
}

# The mirror pages: the same cross of four arrows (FMT companion's layout).
for page, prefix in [("Rétro gauche", "Rétro gauche"), ("Rétro droit", "Rétro droit"), ("Rétro central", "Position rétro central"),
                     ("Rétros tous", "Rétros tous"), ("Largeur", "Largeur des rétros"), ("Angle central", "Rétro central")]:
    PAGES[page] = {
        "0,0": back(),
        "2,0": role(f"{P}{prefix} haut", "up", page, "haut", "post"), "2,2": role(f"{P}{prefix} bas", "down", page, "bas", "post"),
        "1,1": role(f"{P}{prefix} gauche", None, page, "gauche", "post"), "3,1": role(f"{P}{prefix} droite", None, page, "droite", "post"),
    }

# The pages a folder opens: each page's parent is where its folder key is.
CHILDREN = {"Accueil": ["Voiture", "Stand", "Pneus", "Caméra", "Poste"], "Voiture": ["Voiture suite"], "Voiture suite": ["Antiroulis"],
            "Stand": ["Pneus", "Aéro"], "Caméra": ["Swingman", "Caméra libre", "Replay"], "Caméra libre": ["Objectif"],
            "Poste": ["Rétros"], "Rétros": ["Rétro gauche", "Rétro droit", "Rétro central", "Rétros tous", "Largeur", "Angle central"]}



def build():
    p = Profile()
    root_id = "8F3A6C2E-5B1D-4E7A-9C40-2D6B1E0F7A11"
    default_page = pid("défaut")
    manifests = {}
    for name, keys in PAGES.items():
        actions = {pos: make(p) for pos, make in keys.items()}
        for c in range(5):
            for r in range(3):
                actions.setdefault(f"{c},{r}", None)
        actions = {k: v for k, v in actions.items() if v is not None}
        for pos, a in actions.items():
            # The same key on two pages (Freins, Réparations…) stays two buttons: own ids per page and place.
            if "EMPreview" in a:
                salt = name + "/" + pos + "/"
                a["Settings"] = json.loads(re.sub(r'"([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})"',
                                                  lambda m: '"' + str(uuid.uuid5(uuid.NAMESPACE_URL, salt + m.group(1))) + '"',
                                                  json.dumps(a["Settings"], ensure_ascii=False)))
            a["ActionID"] = str(uuid.uuid5(uuid.NAMESPACE_URL, name + "/" + pos + "/" + a["ActionID"]))
            p.preview[a["ActionID"]] = a.pop("EMPreview", {})
        manifests[pid(name)] = {"Controllers": [{"Actions": actions, "Type": "Keypad"}], "Icon": "", "Name": name}
    manifests[default_page] = {"Controllers": [{"Actions": None, "Type": "Keypad"}], "Icon": "", "Name": ""}
    root = {"Pages": {"Current": pid("Accueil"), "Default": default_page, "Pages": [pid("Accueil")]},
            "Name": "Endurance Manager · FMT", "Version": "3.0", "Device": {"Model": "20GBA9901", "UUID": ""}}
    package = {"DeviceModel": "20GBA9901", "OSType": "Windows", "DeviceSettings": None, "OSVersion": "10.0",
               "RequiredPlugins": ["com.wotever.simhubdynamicbutton"], "AppVersion": "7.6.0.22849", "FormatVersion": 1}
    base = f"Profiles/{root_id}.sdProfile/"
    with zipfile.ZipFile(OUT, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("package.json", json.dumps(package, indent=2, ensure_ascii=False))
        z.writestr(base + "manifest.json", json.dumps(root, ensure_ascii=False))
        for page_id, m in manifests.items():
            folder_path = base + "Profiles/" + page_id.upper() + "/"
            z.writestr(folder_path + "manifest.json", json.dumps(m, ensure_ascii=False))
            used = set()
            for a in (m["Controllers"][0]["Actions"] or {}).values():
                for s in a["States"]:
                    used.add(s["Image"])
            for img in used:
                z.writestr(folder_path + img, p.images[img])
    # A preview of each page for the docs and the PR (pages side by side), from the same key images.
    return p, manifests


# What the live titles show in the preview (Stream Deck draws them over the key images).
SAMPLE = {EM + "Profils.1.Titre": "Sec", EM + "Profils.2.Titre": "Inter\nconseillé", EM + "Profils.3.Titre": "Pluie", GD + "PitLimiterOn": "0",
          GD + "HeadlightsActive": "1", GD + "WipersActive": "0", FMT + "Pit.Brakes.Short": "Garder", FMT + "Pit.Damage.Short": "Toutes",
          EM + "Voiture.Reglages.TC": "4", EM + "Voiture.Reglages.CoupureTC": "3", EM + "Voiture.Reglages.GlisseTC": "2", EM + "Voiture.Reglages.Regen": "80 kW", EM + "Voiture.Reglages.Migration": "2", EM + "Voiture.Reglages.AntiroulisAV": "5", EM + "Voiture.Reglages.AntiroulisAR": "4", EM + "Voiture.Reglages.ABS": "3", EM + "Voiture.Reglages.Carto": "2", EM + "Profils.Actif": "Sec", EM + "Voiture.Reglages.Repartition": "54,2",
          FMT + "Pit.VE.Percent": "85", FMT + "Pit.Ratio": "0,16", FMT + "Pit.FLPRESS": "131", FMT + "Pit.Driver.Short": "N. Boul.",
          FMT + "Pit.Tyre.FL.Badge": "MED", FMT + "Pit.Tyre.FR.Badge": "MED", FMT + "Pit.Tyre.RL.Badge": "MED", FMT + "Pit.Tyre.RR.Badge": "MED",
          FMT + "Pit.Tyres.Badge": "MED", FMT + "Pit.RWING": "P10", FMT + "Pit.GRILLE": "5", FMT + "Pit.FBRAKEDUCT": "Ouvert", FMT + "Pit.RBRAKEDUCT": "20 %"}
ON = {EM + "Profils.1.Actif", GD + "HeadlightsActive"}


def preview(p, manifests, path):
    from PIL import Image, ImageDraw, ImageFont
    font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 22)
    order = ["Accueil", "Voiture", "Stand", "Pneus", "Voiture suite", "Aéro", "Caméra", "Poste"]
    tiles = []
    for name in order:
        m = manifests[pid(name)]
        sheet = Image.new("RGB", (5 * 152 + 8, 3 * 152 + 8), "#030506")
        for pos, a in (m["Controllers"][0]["Actions"] or {}).items():
            c, r = map(int, pos.split(","))
            meta = p.preview.get(a["ActionID"], {})
            on = meta.get("on") and meta.get("on_when", "").strip("[]") in ON
            img = Image.open(io.BytesIO(p.images[meta["on"] if on else a["States"][0]["Image"]])).convert("RGB")
            prop = re.sub(r"^format\(\[|^\[|\].*$", "", meta.get("formula") or "")
            title = SAMPLE.get(prop, "")
            if title:
                d = ImageDraw.Draw(img)
                lines = title.split("\n")
                y = 144 - 14 - 26 * len(lines)
                for line in lines:
                    w = d.textlength(line, font=font)
                    d.text(((144 - w) / 2, y), line, font=font, fill="#04170B" if on else "#FFFFFF")
                    y += 26
            sheet.paste(img, (8 + c * 152, 8 + r * 152))
        tiles.append(sheet)
    w, h = tiles[0].size
    out = Image.new("RGB", (2 * w + 24, 4 * h + 72), "#07090A")
    for i, t in enumerate(tiles):
        out.paste(t, ((i % 2) * (w + 24), (i // 2) * (h + 24)))
    out.save(path)


if __name__ == "__main__":
    prof, mans = build()
    preview(prof, mans, os.path.join(HERE, "apercu.png"))
    print("écrit :", OUT)
