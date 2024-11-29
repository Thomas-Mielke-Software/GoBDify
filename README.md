# GoBDify -- eine Hilfs-App für die GoBD-konforme Dokumentenarchivierung

Plattform: .NET MAUI

## Konzept

GoBDify nutzt sogenannte *hashes*, um ein GoBD-konformes Dokumentenmanagement zu erleichtern. Hashes sind kryptographisch sichere 'Quersummen' über Daten, d.h. sie lassen sich nicht durch gezielte Änderungen an den Daten reproduzieren. Damit können hashes dem Ziel der Veränderungssicherheit hilfreich sein. Es bedarf aber einer zusätzlichen Sicherheit, die verhindert, dass hashes nicht einfach neu erzeugt werden können.

Konkret baut die App sha256 hashes aus den zu archivierenden Dateien, z.B. in einem Cloud-Verzeichnis, das mit einem Ordner-öffnen-Dialog ausgewählt wird, und speichert deren hashes in einer .sha256-Dateie, die mit `sha256sum -c XXX.sha256` überprüft werden kann. Für die GoBD-Bedingung der veränderungssicheren Speicherung wird in einem zweiten Schritt ein hash auf die im ersten Schritt erstellte .sha256-Datei angefertigt und an eine Zertifizierungsautorität gesandt, die bestätig, dass der hash zu einem bestimmten Zeitpunkt einen bestimmten Wert hat (timestamping). Manipulationen der archivierten Dokumente führen also dazu, dass der entsprechende hash in der .sha256-Datei nicht mehr stimmt; und wird der hash des Dokuments in der .sha256-Datei geändert, stimmt  wiederum deren hash nicht mehr, wobei ein erneutes hashen nur zusammen mit der Erzeugung eines neuen timestamp möglich ist, was bei einer Buchprüfung auffallen dürfte. Neben den Archivdokumenten wird auch die jeweils letzte erzeugte .sha256-Datei gehasht, so dass sich eine art Blockchain bildet.

Voraussetzung für GoBD-Konformität ist allerdings, dass man die Dokumente zeitnah hasht, z.B. auch wenn man unterwegs ist. Deshalb wurde als Implementationsform eine mobile App gewählt.

## Lizenz

GNU Affero GPL v3.0