using CommunityToolkit.Maui.Storage;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System;
using System.Threading;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Xml.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Net.Http;
using Microsoft.Maui.Storage;
using System.Security.Cryptography.Pkcs;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Maui.Controls.PlatformConfiguration;

namespace GoBDify;

public partial class MainPage : ContentPage
{

    public MainPage()
    {
        InitializeComponent();
        SetFolderPath(Preferences.Get("DefaultFolder", null));
    }

    private void SetFolderPath(string? path)
    {
        if (path == null || path == "")
        {
            folderSelectBtn.Text = "Bitte Ordner auswählen";
            timestampBtn.IsEnabled = false;
        }
        else
        {
            folderSelectBtn.Text = $"'{path}' ändern";
            timestampBtn.IsEnabled = true;
        }
    }

    private async void OnFolderClicked(object sender, EventArgs e)
    {
        try
        {
            // Ordnerauswahl-Dialog
            FolderPickerResult folder;
            string? folderPath = Preferences.Get("DefaultFolder", null);
            if (folderPath == null)
                folder = await FolderPicker.PickAsync(default);
            else
                folder = await FolderPicker.PickAsync(folderPath);
            if (folder?.Folder?.Path == null)  // abgebrochen?
            {
                if (folder.Exception != null)
                    DisplayAlert("Problem bei Ordnerauswahl", $"Bitte versuchen einen lokalen Ordner auzuwählen. Mit Cloud Storage gibt es derzeit noch ein paar Probleme. Ursprüngliche Meldung:  {folder.Exception.Message}", "Abbrechen");
                progressBar.IsVisible = false;
                return;
            }
            folderPath = folder?.Folder.Path;
            Preferences.Set("DefaultFolder", folderPath);
            SetFolderPath(folderPath);
#if WINDOWS
            if (folderPath != null)
            {
                var windowsStorageFolder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(folderPath);
                Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", windowsStorageFolder);
            }
#endif
        }
        catch
        {
        }
    }

    private async void OnTimestampClicked(object sender, EventArgs e)
    {
        try
        {
            progressBar.Progress = 0;
            progressBar.IsVisible = true;
            outputEdt.Text = "";

            PermissionStatus statusread = await Permissions.RequestAsync<Permissions.StorageRead>();
            PermissionStatus statuswrite = await Permissions.RequestAsync<Permissions.StorageWrite>();

            string? folderPath = Preferences.Get("DefaultFolder", null);

            // Ordnerdateien einlesen
            DirectoryInfo d = new DirectoryInfo(folderPath);
            FileInfo[] allFiles = d.GetFiles("*");
            if (allFiles.Length == 0)
            {
                OutputLine("ABGEBROCHEN: ORDNER IST LEER");
                progressBar.Progress = 1;
            }
            else 
            { 
                var filesWithHash = new List<FileInfo>();
                var filesWithoutHash = new List<FileInfo>();
                double incrementProgress = 0.5 / (double)allFiles.Length;
                string hash;

                // existierende hashes prüfen
                int lastSha256File = 0;
                foreach (FileInfo file in allFiles)
                {
                    // Datei entspricht dem Muster 'timestamp#####.sha256'?
                    if (file.Name.Length == 21 && file.Name.StartsWith("timestamp") && file.Name.EndsWith(".sha256") && file.Name.Substring(9, 5).All(char.IsDigit))
                    {
                        string tstFilename = file.Name + ".tst";
                        string tstFileFullname = file.FullName + ".tst";
                        string validText = "";
                        string timestampText = "UNGÜLTIGER ZEITSTEMPEL";
                        bool isValid = false;
                        X509Certificate2? x509cert = null;

                        // validiere existierenden .tst timestamp
                        try
                        {
                            string metahash = await HashFile(file.FullName);  // hash des hashfile

                            // Rfc3161TimestampRequest dummy instance erzeugen
                            Rfc3161TimestampRequest timestampRequest =
                                Rfc3161TimestampRequest.CreateFromHash(
                                    Convert.FromHexString(metahash),
                                    HashAlgorithmName.SHA256,
                                    null,
                                    null,  // nounce hier nicht nötig: aus dem ursprünglichen response ziehen, der hier gleich aus der Datei gelesen wird
                                    true);

                                byte[] timestampResponseBytes = File.ReadAllBytes(tstFileFullname);
                                int bytesConsumed;
                                Rfc3161TimestampToken timestampToken =
                                    timestampRequest.ProcessResponse(timestampResponseBytes, out bytesConsumed);

                                // Timestamp prüfen
                                isValid = timestampToken.VerifySignatureForHash(Convert.FromHexString(metahash), HashAlgorithmName.SHA256, out x509cert);
                                timestampText = "vom " + timestampToken.TokenInfo.Timestamp.ToString();
                            }
                            catch 
                            {
                                validText = $"KONNTE {tstFilename} NICHT LESEN!";
                        }
                        if (x509cert == null || x509cert.IssuerName.Name.Length == 0)
                            validText = "UNGÜLTIGE SIGNATUR";
                        else
                            validText = isValid ? $"gültige Signatur von {x509cert.IssuerName.Name}" : $"UNGÜLTIGE SIGNATUR VON {x509cert.IssuerName.Name}";
                        OutputLine($"___ {file.Name} {timestampText} {validText} ___");
                        
                        int n;
                        int.TryParse(file.Name.Substring(9, 5), out n);  // mitzählen
                        if (n > lastSha256File)                          // und letzte Nummer ermitteln
                            lastSha256File = n;

                        const int BufferSize = 128;
                        using (var fileStream = File.OpenRead(file.FullName))
                        using (var streamReader = new StreamReader(fileStream, Encoding.UTF8, true, BufferSize))
                        {
                            string line;
                            while ((line = streamReader.ReadLine()) != null)
                            {
                                if (line[0] != '#' && line.Length > 66)
                                {
                                    string lineFilename = line.Substring(66);
                                    var lineFile = allFiles.Where(fi => fi.Name == lineFilename).FirstOrDefault();
                                    if (lineFile != null)
                                    {
                                        if (!filesWithHash.Any(fi => fi.Name == lineFilename))
                                            filesWithHash.Add(lineFile);
                                        string path = Path.Combine(folderPath, lineFilename);
                                        try
                                        {
                                            hash = await HashFile(path);
                                            if (hash == line.Substring(0, 64))
                                                OutputLine($"OK: {lineFilename}");
                                            else
                                                OutputLine($"VERÄNDERT: {lineFilename}");
                                        }
                                        catch (FileNotFoundException ex)
                                        {
                                            OutputLine($"DATEI NICHT GEFUNDEN: {lineFilename}");
                                        }
                                    }
                                }
                            }
                        }
                    }

                    progressBar.Progress += incrementProgress;
                }

                // Dateien ermitteln, für die keine Hashes existieren
                filesWithoutHash = allFiles.Where(p => !filesWithHash.Any(p2 => p2.Name == p.Name)).ToList();

                if (lastSha256File >= 99999)
                {
                    OutputLine("ABGEBROCHEN: MAXIMALE ANZAHL AN TIMESTAMPS IN DIESEM ORDNER ERREICHT");
                    progressBar.Progress = 1;
                }
                else if (filesWithoutHash.Count == 0 || (filesWithoutHash.Count <= 2 &&
                    filesWithoutHash.All(fi => fi.Name == $"timestamp{lastSha256File:D5}.sha256" ||      // keine neuen Dateien hinzugekommen
                                               fi.Name == $"timestamp{lastSha256File:D5}.sha256.tst")))  // außer dem letzen timestamp?
                {
                    OutputLine("\nKEINE NEUEN DATEIEN HINZUGEKOMMEN SEIT LETZTEM TIMESTAMP");
                    progressBar.Progress = 1;
                }
                else
                {
                    // ansonsten hashes für neue Dateien erstellen
                    var shaFileContent = new StringBuilder();
                    incrementProgress = 0.5 / (double)filesWithoutHash.Count;
                    foreach (FileInfo file in filesWithoutHash)
                    {
                        hash = await HashFile(file.FullName);

                        // .sha256 Zeile hinzufügen
                        shaFileContent.Append(hash);
                        shaFileContent.Append(" *");
                        shaFileContent.Append(file.Name);
                        shaFileContent.Append("\n");        // Unix-Zeilenumbruch benötigt

                        OutputLine($"NEU: {file.Name}");

                        progressBar.Progress += incrementProgress;
                    }

                    // instruktiven Kommentar-Header vorweg
                    var shaFileHeader = new StringBuilder();
                    shaFileHeader.Append($"# Dokument-Hashes überprüfen\n");
                    shaFileHeader.Append($"#   - unter Linux: sha256sum -c timestamp{lastSha256File + 1:D5}.sha256\n");
                    shaFileHeader.Append($"#   - auf macOS: shasum -a 256 -c timestamp{lastSha256File + 1:D5}.sha256\n");
                    shaFileHeader.Append($"#   - in der Windows Powershell (nur einzelnen Datei-Hash): Get-Filehash zu_checkende_datei.pdf -Algorithm SHA256\n");
                    shaFileHeader.Append($"# Timestamp dieser .sha256-Datei verifizieren:\n");
                    shaFileHeader.Append($"#   - unter Linux: openssl ts -verify -data timestamp{lastSha256File + 1:D5}.sha256 -in timestamp{lastSha256File + 1:D5}.sha256.tst -CApath \"$(openssl version -d | cut -d '\"' -f 2)/certs/\"\n");

                    // hashes anfügen
                    string sha256Filename = $"timestamp{lastSha256File + 1:D5}.sha256";
                    string sha256Path = Path.Combine(folderPath, sha256Filename);
                    File.WriteAllText(sha256Path, shaFileHeader.ToString() + shaFileContent.ToString());
                    OutputLine($"NEUER TIMESTAMP: {sha256Filename}");

                    // hash der hashes-Datei erstellen
                    hash = await HashFile(sha256Path);

                    // timestamp von CA beziehen
                    var timestamp = await Timestamping.RequestTimestampTokenForHash(hash: Convert.FromHexString(hash), hashAlgorithmName: HashAlgorithmName.SHA256);
                    var timestampToken = timestamp.token;
                    var responseBytes = timestamp.rawResponse;
                    string tstPath = Path.Combine(folderPath, $"timestamp{lastSha256File + 1:D5}.sha256.tst");
                    File.WriteAllBytes(tstPath, responseBytes);
                    //File.WriteAllBytes(tstPath, timestampToken.TokenInfo.Encode());            

                    // timestamp überprüfen
                    //X509Certificate2 x509cert;
                    //bool isValid = timestampToken.VerifySignatureForHash(Convert.FromHexString(hash), HashAlgorithmName.SHA256, out x509cert);

                    //if (isValid)
                    //{
                    //    folderPath = $"Timestamp token is valid for {timestampToken.TokenInfo.Timestamp.ToString()}";
                    //}
                    //else
                    //{
                    //    folderPath = $"Timestamp token is invalid for {timestampToken.TokenInfo.Timestamp.ToString()}";
                    //}
                }
            }
        }
        catch (Exception ex)
        {
            OutputLine($"VERARBEITUNG ABGEBROCHEN: {ex.Message}");
            progressBar.IsVisible = false;
        }
        await outputScrollview.ScrollToAsync(outputEnd, Microsoft.Maui.Controls.ScrollToPosition.End, true);
    }

    private async void OutputLine(string line)
    {
        outputEdt.Text += $"{line}\n"; 
        await outputScrollview.ScrollToAsync(outputEdt, Microsoft.Maui.Controls.ScrollToPosition.End, true);
    }

    private async Task<string> HashFile(string file)
    {
        using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None, bufferSize: 4096, useAsync: true))
        using (var sha256 = SHA256.Create())
        {
            var buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
            {
                sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
            }
            sha256.TransformFinalBlock(buffer, 0, 0);
            return BitConverter.ToString(sha256.Hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
