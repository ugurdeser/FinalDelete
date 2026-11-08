# FileLockFinder

Windows'ta bir dosyayı silmeye çalışırken çıkan **"Dosya kullanımda / File In Use"** uyarısında, dosyayı **hangi programın** kullandığını anında gösteren küçük bir arka plan uygulaması.

*A small background app for Windows: when Explorer shows the "File In Use" warning, it instantly shows which program is holding the file.*

## Özellikler / Features
- Sistem tepsisinde çalışır, uyarı penceresini olay tabanlı yakalar (sürekli tarama yok).
- Dosyalar için Windows Restart Manager API'si; klasörler için içindeki dosyalar + çalışma dizini o klasörde olan programlar (ör. açık terminal).
- Sonuç penceresi: program adı, PID, neden, exe yolu — **İşlemi Sonlandır** / **Konumu Aç**.
- Tepsi menüsü: elle dosya/klasör kontrolü, Windows ile başlat, **Dil / Language** (Türkçe / English).

## Derleme / Build
```
dotnet build -c Release
```
Çıktı / Output: `bin\Release\net10.0-windows\FileLockFinder.exe` (.NET 10 Desktop Runtime gerekir / required)

Yönetici yetkisiyle çalışan programları sonlandırmak için uygulamayı yönetici olarak başlatın.
*Run as administrator to end elevated processes.*
