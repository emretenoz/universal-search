namespace UniversalSearch.App;
public sealed class Strings(bool english)
{
    public string this[string key] => Data.TryGetValue(key, out var v) ? english ? v.en : v.tr : key;
    static readonly Dictionary<string,(string tr,string en)> Data = new()
    {
        ["Preview"]=("Hızlı önizleme", "Quick preview"), ["PreviewHint"]=("Space / Esc kapat    Enter aç", "Space / Esc close    Enter open"), ["PreviewLoading"]=("Yükleniyor…", "Loading…"), ["PreviewUnsupported"]=("Önizleme kullanılamıyor. Enter ile varsayılan uygulamada açabilirsin.", "Preview unavailable. Press Enter to open in the default app."),
                ["QuickFolders"]=("Hızlı ekle", "Quick add"),
        ["Desktop"]=("Masaüstü", "Desktop"), ["Documents"]=("Belgeler", "Documents"), ["Pictures"]=("Resimler", "Pictures"),
        ["DriveHint"]=("Konumlar yalnızca Kaydet ve indeksle ile taranmaya başlar. Tüm diskin ilk taraması uzun sürebilir. Sistem ve önbellek klasörlerini aşağıdan hariç tutabilirsiniz. Gösterilen sayı taranan kayıttır; yüzde değildir.", "Locations are scanned only after Save and index. The first full-drive scan may take a while. Exclude system and cache folders below. The count shows scanned entries, not a percentage."),
        ["Opening"]=("Açılıyor…", "Opening…"), ["OpenFailed"]=("Açılamadı. Dosyanın kullanılabilir olduğundan ve varsayılan uygulamanın ayarlandığından emin olun.", "Could not open. Check that the file is available and a default app is configured."), ["HistorySaveFailed"]=("Öğe açıldı; kullanım geçmişi kaydedilemedi.", "Item opened; usage history could not be saved."),
        ["General"]=("Genel", "General"), ["Locations"]=("Arama konumları", "Search locations"), ["WebSettings"]=("Web araması", "Web search"),
        ["GeneralIntro"]=("Aramayı sana göre düzenle.", "Make search feel like yours."),
        ["LocationsIntro"]=("Hangi dosya ve klasörlerin bulunacağını seç.", "Choose the files and folders you want to find."),
        ["WebIntro"]=("Bir anahtar yaz, aramanı tarayıcıda aç.", "Type a keyword to search in your browser."),
        ["PrivacyIntro"]=("Verilerin bu bilgisayarda kalır. Kontrol sende.", "Your data stays on this PC. You stay in control."),
        ["Appearance"]=("Görünüm", "Appearance"), ["LightTheme"]=("Açık · Gün ışığı", "Light · Daylight"), ["DarkTheme"]=("Koyu · Gece", "Dark · Midnight"),
        ["SettingsDraft"]=("Değişiklikler kaydettiğinde uygulanır.", "Changes apply when you save."), ["SaveChanges"]=("Değişiklikleri kaydet", "Save changes"),
        ["BrowserSection"]=("Tarayıcı geçmişi", "Browser history"), ["LocalData"]=("Yerel veriler", "Local data"),
        ["LocalDataIntro"]=("Geçmişi veya arama indeksini temizleyebilirsin. Kendi dosyaların silinmez.", "Clear history or the search index. Your own files are never deleted."),
        ["WebExample"]=("Örnek: g | Google | https://www.google.com/search?q={query}", "Example: g | Google | https://www.google.com/search?q={query}"),
        ["ClearSearch"]=("Aramayı temizle", "Clear search"),
        ["Search"]=("Ara…","Search…"),
        ["Settings"]=("Ayarlar","Settings"), ["Hint"]=("↑ ↓ gezin    Space önizle    Enter aç    Esc gizle","↑ ↓ navigate    Space preview    Enter open    Esc hide"),
        ["All"]=("Tümü","All"),["Files"]=("Dosyalar","Files"),["Apps"]=("Uygulamalar","Apps"),["Folders"]=("Klasörler","Folders"),["Favorites"]=("Favoriler","Favorites"),
        ["Ready"]=("Hazır","Ready"),["Indexing"]=("İndeksleniyor","Indexing"),["Partial"]=("Bazı konumlara erişilemedi","Some locations are inaccessible"),
        ["Error"]=("İşlem tamamlanamadı. Tekrar deneyin; ayrıntılar yerel günlükte.","Could not complete the action. Retry; details are in the local log."),
        ["Empty"]=("Sonuç bulunamadı. Daha kısa bir arama veya farklı filtre deneyin.","No results. Try a shorter query or different filters."),
        ["Welcome"]=("Arama alanını kendin belirle","Make room for what matters"),
        ["WelcomeBody"]=("Başlat menüsü hazır. Dosyaların için Ayarlar'dan klasör veya disk ekle. İçerikler bilgisayarında kalır.","Your Start menu is ready. Add folders or drives in Settings to find your files. Your data stays on this PC."),
        ["OpenLocation"]=("Dosya konumunu aç","Open file location"),["CopyPath"]=("Yolu kopyala","Copy path"),["Favorite"]=("Favoriye ekle / çıkar","Toggle favorite"),
        ["Roots"]=("İndekslenecek klasörler / diskler","Folders / drives to index"),["Exclusions"]=("Hariç tutulacak dizinler","Excluded directories"),
        ["Add"]=("Klasör ekle","Add folder"),["Remove"]=("Seçileni kaldır","Remove selected"),
        ["Privacy"]=("Gizlilik ve davranış","Privacy and behavior"),["Browser"]=("Chrome, Edge ve Firefox geçmişimi yerel olarak indeksle (izin ver)","Index my Chrome, Edge and Firefox history locally (consent)"),
        ["Startup"]=("Windows açıldığında başlat","Launch at Windows sign-in"),["Hotkey"]=("Kısayol (ör. Alt+Space veya Ctrl+Shift+F)","Hotkey (e.g. Alt+Space or Ctrl+Shift+F)"),
        ["Language"]=("Dil / Language","Language / Dil"),["WebEngines"]=("Web motorları: anahtar | ad | HTTPS adresi ({query} içermeli)","Web engines: keyword | name | HTTPS URL (must contain {query})"),
        ["Save"]=("Kaydet ve indeksle","Save and index"),["Cancel"]=("Vazgeç","Cancel"),["ClearHistory"]=("Arama geçmişini ve kullanımı temizle","Clear searches and usage"),
        ["ClearIndex"]=("İndeksi temizle ve duraklat","Clear index and pause"),["ConfirmClear"]=("Yerel kayıtlar temizlensin mi? Dosyalarınız silinmez.","Clear local records? Your files will not be deleted."),
        ["Paused"]=("İndeksleme duraklatıldı. Ayarları kaydederek yeniden başlatın.","Indexing paused. Save settings to restart."),
        ["InvalidSettings"]=("Ayarları kontrol edin: geçerli klasörler, kısayol ve web adresleri gerekli.","Check your settings: valid folders, hotkey and web URLs are required."),
        ["HotkeyBusy"]=("Kısayol başka bir uygulamada kullanılıyor. Ayarlardan değiştirin.","The hotkey is in use. Change it in Settings."),
        ["Recent"]=("Son aramalar","Recent searches"),["Quit"]=("Çıkış","Quit"),["Show"]=("Aramayı aç","Open search"),
        ["Calculator"]=("Enter ile sonucu kopyala","Press Enter to copy result"),["BrowserUnavailable"]=("Bazı tarayıcı geçmişleri okunamadı. Tarayıcıları kapatıp ayarları yeniden kaydedin.","Some browser history could not be read. Close browsers and save settings again."),
        ["Help"]=("Filtreler: ext:pdf  in:\"C:\\İş\"  type:folder  is:favorite  •  Komutlar: >  •  Web: g arama","Filters: ext:pdf  in:\"C:\\Work\"  type:folder  is:favorite  •  Commands: >  •  Web: g query"),
        ["Reindex"]=("İndeksi güncelle","Refresh index"),["Copy"]=("Kopyala","Copy"),
        ["File"]=("Dosya","File"),["Folder"]=("Klasör","Folder"),["Application"]=("Uygulama","Application"),["History"]=("Tarayıcı geçmişi","Browser history"),["Web"]=("Web","Web")
    };
}








