# Launch RimWorld with the repo mods + their dependencies loaded.
#
# `just run` symlinks both repo mods into the game's Mods/ dir (so the game
# always sees the current working tree), writes a known-good ModsConfig.xml
# with all dependencies in load order, then starts the game.

data_home   := env('XDG_DATA_HOME', home_directory() / ".local/share")
config_home := env('XDG_CONFIG_HOME', home_directory() / ".config")
steam_common := data_home / "Steam/steamapps/common"
game_dir := steam_common / "RimWorld"
runtime  := steam_common / "SteamLinuxRuntime_soldier/_v2-entry-point"
config   := config_home / "unity3d/Ludeon Studios/RimWorld by Ludeon Studios/Config/ModsConfig.xml"
repo     := justfile_directory()

# Link mods, set ModsConfig, launch the game.
# NixOS: steam-run provides the FHS layout pressure-vessel's bwrap needs
# (e.g. /usr/bin/true), then the Steam Linux Runtime (soldier) supplies the
# libs the game itself links against.
run: link mods-config
    steam-run "{{runtime}}" --verb=waitforexitandrun -- "{{game_dir}}/start_RimWorld.sh"

# Symlink the repo's mod folders into the game Mods/ dir, clearing stale copies.
link:
    #!/usr/bin/env nu
    let mods_dir = "{{game_dir}}/Mods"
    # repo mod dir -> name to expose it under in Mods/
    let links = {
        "greyscythe-cybergenetics":       "Greyscythe_Cybergenetics",
        "greyscythe-bionics-catalogue":   "Greyscythe_Bionics",
    }
    for src in ($links | columns) {
        let dst = ($mods_dir | path join ($links | get $src))
        if ($dst | path exists) { rm -rf $dst }
        ln -s ("{{repo}}" | path join $src) $dst
        print $"linked ($dst)"
    }

# Write ModsConfig.xml with deps + repo mods in load order (backs up existing).
mods-config:
    #!/usr/bin/env nu
    let cfg = "{{config}}"
    if ($cfg | path exists) { cp $cfg $"($cfg).bak" }
    let active = [
        brrainz.harmony
        ludeon.rimworld
        ludeon.rimworld.royalty
        ludeon.rimworld.ideology
        ludeon.rimworld.biotech
        ludeon.rimworld.anomaly
        ludeon.rimworld.odyssey
        oskarpotocki.vanillafactionsexpanded.core
        vanillaexpanded.vgeneticse
        feaurie.greyscythegenes
        feaurie.greyscythebionics
        ISOREX.PawnEditor
    ]
    let known = [
        ludeon.rimworld.royalty
        ludeon.rimworld.ideology
        ludeon.rimworld.biotech
        ludeon.rimworld.anomaly
        ludeon.rimworld.odyssey
    ]
    let li = {|xs| $xs | each {|m| $"    <li>($m)</li>" } | str join "\n" }
    let xml = ([
        '<?xml version="1.0" encoding="utf-8"?>'
        '<ModsConfigData>'
        '  <version>1.6</version>'
        '  <activeMods>'
        (do $li $active)
        '  </activeMods>'
        '  <knownExpansions>'
        (do $li $known)
        '  </knownExpansions>'
        '</ModsConfigData>'
    ] | str join "\n")
    $xml | save -f $cfg
    print $"wrote ($cfg)"
