# Déploiement sur desk-organizer.artmonie.com

GitHub reste la source : chaque push sur `main` déclenche GitHub Actions, qui compile
le .exe et l'installateur (Inno Setup, `installer/`) et les publie dans la release `latest`.

Le serveur (fsj-vps, 185.135.137.86) se synchronise tout seul toutes les 10 minutes
(`desk-organizer-sync.timer`) :

- miroir Git du dépôt : `/srv/git/PersonalComputerDeskOrganizer.git`
- page de téléchargement : `/var/www/desk-organizer/` (`web/` ici)
- fichiers téléchargeables : `/var/www/desk-organizer/download/` (Setup.exe, .exe, .exe.gz, .zip, version.json)

| Fichier ici | Sur le serveur |
|---|---|
| `desk-organizer-sync.sh` | `/usr/local/bin/desk-organizer-sync.sh` |
| `desk-organizer-sync.service` / `.timer` | `/etc/systemd/system/` |
| `nginx-desk-organizer.conf` | `/etc/nginx/sites-available/desk-organizer` (certbot y a ajouté le bloc HTTPS : ne pas l'écraser) |
| `web/*` | `/var/www/desk-organizer/` |

Forcer une synchro immédiate : `ssh fsj-vps systemctl start desk-organizer-sync.service`
