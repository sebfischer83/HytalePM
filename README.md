# HytalePM

Package Manager für Hytale Mods (und andere Minecraft-ähnliche Spiele).

## Projekte

### HytalePM.Console

Eine .NET 10 Konsolenanwendung zur Überprüfung von Mod-Versionen gegen CurseForge.

**Features:**
- Liest Konfiguration aus einer JSON-Datei mit Mod-Liste und CurseForge API-Token
- Durchsucht ein angegebenes Verzeichnis nach installierten Mods (.jar Dateien)
- Prüft, ob die Mod-Versionen aktuell sind
- Zeigt an, welche Mods Updates benötigen, mit Download-Links

**Mehr Informationen:** [Source/HytalePM.Console/README.md](Source/HytalePM.Console/README.md)

## Anforderungen

- .NET 10 SDK
- CurseForge API Key (erhältlich unter https://console.curseforge.com/)

## Schnellstart

1. Erstelle eine `config.json` Datei basierend auf `Source/HytalePM.Console/config.json.example`
2. Füge deinen CurseForge API Key ein
3. Konfiguriere die zu überprüfenden Mods
4. Führe die Anwendung aus:

```bash
cd Source/HytalePM.Console
dotnet run -- /pfad/zu/deinen/mods
```

## Lizenz

Dieses Projekt ist Teil des HytalePM-Repositories.
