<p align="center"><img src="Assets/logo.png" width="128" alt="FileLockFinder logo"></p>

# FileLockFinder

[Türkçe](#türkçe) | [English](#english)

---

## Türkçe

Windows'ta bir dosyayı silmeye çalıştığınızda **"Dosya Kullanılıyor"** uyarısı çıkar, ama çoğu zaman dosyayı *hangi programın* kullandığını söylemez. **FileLockFinder** arka planda çalışır ve bu uyarı çıktığı anda, dosyayı kullanan programı yanında küçük bir pencerede gösterir.

### Özellikler
- **Otomatik algılama:** Explorer'ın "Dosya Kullanılıyor / Klasör Kullanılıyor" uyarısını anında yakalar. Olay tabanlı çalışır, sürekli tarama yapmaz, işlemciyi yormaz.
- **Ne gösterir:** Program adı, PID, kullanılan öğe, neden ve programın exe yolu.
- **İşlemi Sonlandır:** Dosyayı tutan programı tek tıkla kapatın (onay sorulur).
- **Konumu Aç:** Programın exe dosyasını Explorer'da gösterir.
- **Klasör desteği:** Klasörün içindeki açık dosyaları ve çalışma dizini o klasörde olan programları (ör. açık bir terminal) bulur.
- **Masaüstü desteği:** Masaüstünden yapılan silme denemelerinde de çalışır.
- **Elle kontrol:** Tepsi menüsünden istediğiniz dosya veya klasörü kontrol edebilirsiniz.
- **Windows ile başlat** seçeneği.
- **Türkçe / İngilizce arayüz:** Varsayılan olarak Windows dilini kullanır, tepsi menüsünden değiştirilebilir.

### Gereksinimler
- Windows 10 / 11 (64 bit)
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (derlemek için .NET 10 SDK)

### Kurulum ve Derleme
```powershell
git clone https://github.com/ugurdeser/FinalDelete.git
cd FinalDelete
dotnet build -c Release
```
Çalıştırılabilir dosya: `bin\Release\net10.0-windows\FileLockFinder.exe`

### Kullanım
1. `FileLockFinder.exe`'yi çalıştırın. Sistem tepsisinde kalkan simgesi belirir.
2. Her zamanki gibi bir dosyayı silmeye çalışın.
3. "Dosya Kullanılıyor" uyarısı çıktığında, yanında dosyayı kullanan programların listesi açılır.
4. İsterseniz programı **İşlemi Sonlandır** ile kapatın, ardından uyarıda **Yeniden Dene**'ye basın.

**Tepsi menüsü (sağ tık):** Dosya kontrol et · Klasör kontrol et · Windows ile başlat · Dil / Language · Çıkış

### Nasıl Çalışır
| Adım | Yöntem |
|---|---|
| Uyarıyı yakalama | `SetWinEventHook` ile Explorer'ın uyarı penceresi (`OperationStatusWindow`) izlenir |
| Hangi dosya? | `Shell.Application` / `IShellWindows` ile Explorer pencerelerinde ve masaüstünde seçili öğeler okunur |
| Kim kullanıyor? | Windows **Restart Manager** API'si (`RmGetList`) |
| Klasörler | İçindeki dosyalar + exe'si veya çalışma dizini (PEB) o klasörde olan işlemler |

### Sınırlamalar
- Yönetici yetkisiyle çalışan programları sonlandırmak için FileLockFinder'ı **yönetici olarak** çalıştırın.
- Silme işlemi Explorer dışından yapılırsa (ör. komut satırı) uyarı penceresi çıkmaz. Bu durumda tepsi menüsünden elle kontrol edin.
- 32 bit programların çalışma dizini okunamaz. Açık dosyaları yine de bulunur.

---

## English

When you try to delete a file in Windows, you often get a **"File In Use"** warning that doesn't tell you *which program* is using it. **FileLockFinder** runs in the background and, the moment that warning appears, shows the program holding the file in a small window right next to it.

### Features
- **Automatic detection:** Instantly catches Explorer's "File In Use / Folder In Use" dialog. Event-driven, no polling, no CPU load.
- **What it shows:** Program name, PID, item in use, reason and executable path.
- **End Process:** Close the program holding the file with one click (with confirmation).
- **Open Location:** Shows the program's executable in Explorer.
- **Folder support:** Finds open files inside a folder and programs whose working directory is in that folder (e.g. an open terminal).
- **Desktop support:** Also works when deleting from the desktop.
- **Manual check:** Check any file or folder from the tray menu.
- **Start with Windows** option.
- **Turkish / English UI:** Uses the Windows display language by default, switchable from the tray menu.

### Requirements
- Windows 10 / 11 (64-bit)
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (.NET 10 SDK to build)

### Build
```powershell
git clone https://github.com/ugurdeser/FinalDelete.git
cd FinalDelete
dotnet build -c Release
```
Executable: `bin\Release\net10.0-windows\FileLockFinder.exe`

### Usage
1. Run `FileLockFinder.exe`. A shield icon appears in the system tray.
2. Delete a file as usual.
3. When the "File In Use" warning appears, a list of the programs using the file opens next to it.
4. Optionally close the program with **End Process**, then click **Try Again** in the warning.

**Tray menu (right-click):** Check file · Check folder · Start with Windows · Dil / Language · Exit

### How It Works
| Step | Method |
|---|---|
| Catching the warning | Explorer's dialog (`OperationStatusWindow`) is watched via `SetWinEventHook` |
| Which file? | Selected items in Explorer windows and on the desktop are read via `Shell.Application` / `IShellWindows` |
| Who is using it? | Windows **Restart Manager** API (`RmGetList`) |
| Folders | Files inside + processes whose executable or working directory (PEB) is in that folder |

### Limitations
- To end programs running as administrator, run FileLockFinder **as administrator**.
- If a delete is attempted outside Explorer (e.g. from the command line), no dialog appears. Use the manual check from the tray menu.
- The working directory of 32-bit programs can't be read. Their open files are still detected.
