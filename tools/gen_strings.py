"""
Source of truth for every user-facing string. Edit the rows below, then run:

    python tools/gen_strings.py

It rewrites src/Obhijatri.App/Strings/bn-BD/Resources.resw and en-US/Resources.resw with the same keys,
and refuses to write if a key is duplicated or a string contains an em or en dash.
"""
import html, os

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "Obhijatri.App", "Strings")

rows = [
    # key, Bangla, English
    ("AppTitle", "অভিযাত্রী", "Obhijatri"),
    ("WindowTitleFormat", "{0} - {1}", "{0} - {1}"),
    ("PrivateWindowTitleFormat", "প্রাইভেট: {0}", "Private: {0}"),
    ("PrivateBadge", "প্রাইভেট", "Private"),
    ("TabStripName", "ট্যাব", "Tabs"),
    ("NewTabTitle", "নতুন ট্যাব", "New tab"),
    ("CloseTabTooltip", "ট্যাব বন্ধ করুন (Ctrl+W)", "Close tab (Ctrl+W)"),

    ("BackButtonTooltip", "পেছনে যান (Alt+বাম তীর)", "Back (Alt+Left)"),
    ("ForwardButtonTooltip", "সামনে যান (Alt+ডান তীর)", "Forward (Alt+Right)"),
    ("ReloadButtonTooltip", "রিলোড করুন (F5)", "Reload (F5)"),
    ("StopButtonTooltip", "লোড হওয়া থামান", "Stop loading"),
    ("HomeButtonTooltip", "হোম পেজ (Alt+Home)", "Home page (Alt+Home)"),
    ("DownloadsButtonTooltip", "ডাউনলোড (Ctrl+J)", "Downloads (Ctrl+J)"),
    ("MenuButtonTooltip", "আরও অপশন", "More options"),
    ("AddressBarPlaceholder", "ওয়েবসাইটের ঠিকানা লিখুন বা সার্চ করুন", "Type a web address or search"),
    ("AddressBarName", "অ্যাড্রেস বার", "Address bar"),

    ("MenuNewTab", "নতুন ট্যাব (Ctrl+T)", "New tab (Ctrl+T)"),
    ("MenuNewPrivateWindow", "নতুন প্রাইভেট উইন্ডো (Ctrl+Shift+N)", "New private window (Ctrl+Shift+N)"),
    ("MenuHistory", "হিস্ট্রি (Ctrl+H)", "History (Ctrl+H)"),
    ("MenuDownloads", "ডাউনলোড (Ctrl+J)", "Downloads (Ctrl+J)"),
    ("MenuImportBookmarks", "অন্য ব্রাউজার থেকে বুকমার্ক ইমপোর্ট করুন", "Import bookmarks from another browser"),
    ("MenuShowBookmarkBar", "বুকমার্ক বার দেখান (Ctrl+Shift+B)", "Show bookmark bar (Ctrl+Shift+B)"),
    ("MenuVerticalTabs", "ট্যাব পাশে দেখান", "Show tabs on the side"),

    ("EngineErrorTitle", "ব্রাউজার ইঞ্জিন চালু হয়নি", "The browser engine did not start"),
    ("EngineErrorMessage", "Microsoft WebView2 Runtime ইনস্টল করা আছে কিনা দেখুন, তারপর অভিযাত্রী আবার চালু করুন।", "Check that the Microsoft WebView2 Runtime is installed, then restart Obhijatri."),
    ("PageCrashedTitle", "পেজটি ঠিকমতো চলছে না", "This page stopped working"),
    ("PageCrashedMessage", "রিলোড বাটনে চাপুন।", "Press Reload to try again."),
    ("DatabaseErrorTitle", "হিস্ট্রি ও বুকমার্ক সেভ করা যাচ্ছে না", "History and bookmarks cannot be saved"),
    ("DatabaseErrorMessage", "ডেটা ফাইলটি খোলা যায়নি। এবার যা করবেন তা সেভ হবে না।", "The data file could not be opened. Nothing from this session will be saved."),
    ("DialogCancel", "বাতিল", "Cancel"),
    ("DialogSave", "সেভ করুন", "Save"),

    ("HistoryTitle", "হিস্ট্রি", "History"),
    ("HistorySearchPlaceholder", "হিস্ট্রিতে সার্চ করুন", "Search history"),
    ("HistoryClearButton", "হিস্ট্রি মুছুন", "Clear history"),
    ("HistoryClearHour", "শেষ এক ঘণ্টা", "Last hour"),
    ("HistoryClearDay", "শেষ ২৪ ঘণ্টা", "Last 24 hours"),
    ("HistoryClearAll", "পুরো হিস্ট্রি", "All history"),
    ("HistoryEmpty", "এখনো কোনো হিস্ট্রি নেই।", "No history yet."),
    ("HistoryNoMatches", "কিছু পাওয়া যায়নি।", "Nothing found."),
    ("HistoryDeleteEntry", "হিস্ট্রি থেকে মুছুন", "Remove from history"),
    ("HistoryConfirmTitle", "হিস্ট্রি মুছবেন?", "Clear history?"),
    ("HistoryConfirmHour", "শেষ এক ঘণ্টায় দেখা সব পেজ হিস্ট্রি থেকে মুছে যাবে।", "Pages visited in the last hour will be removed from history."),
    ("HistoryConfirmDay", "শেষ ২৪ ঘণ্টায় দেখা সব পেজ হিস্ট্রি থেকে মুছে যাবে।", "Pages visited in the last 24 hours will be removed from history."),
    ("HistoryConfirmAll", "পুরো হিস্ট্রি মুছে যাবে। এটি আর ফেরানো যাবে না।", "All history will be removed. This cannot be undone."),
    ("HistoryConfirmYes", "মুছে ফেলুন", "Clear"),

    ("DownloadsTitle", "ডাউনলোড", "Downloads"),
    ("DownloadsEmpty", "এখনো কিছু ডাউনলোড হয়নি।", "Nothing downloaded yet."),
    ("DownloadOpen", "খুলুন", "Open"),
    ("DownloadShowInFolder", "ফোল্ডারে দেখান", "Show in folder"),
    ("DownloadCancelButton", "বাতিল করুন", "Cancel"),
    ("DownloadProgressFormat", "{0} / {1}", "{0} of {1}"),
    ("DownloadCompletedFormat", "ডাউনলোড শেষ, {0}", "Done, {0}"),
    ("DownloadCanceled", "বাতিল করা হয়েছে", "Canceled"),
    ("DownloadFailed", "ডাউনলোড ব্যর্থ হয়েছে", "Download failed"),
    ("SizeBytesFormat", "{0} বাইট", "{0} bytes"),
    ("SizeKbFormat", "{0} কেবি", "{0} KB"),
    ("SizeMbFormat", "{0} এমবি", "{0} MB"),
    ("SizeGbFormat", "{0} জিবি", "{0} GB"),

    ("BookmarkBarEmpty", "পছন্দের পেজ এখানে রাখতে অ্যাড্রেস বারের পাশের স্টার (☆) চিহ্নে চাপুন।", "To keep a page here, press the star next to the address bar."),
    ("BookmarkAddTooltip", "এই পেজ বুকমার্ক করুন (Ctrl+D)", "Bookmark this page (Ctrl+D)"),
    ("BookmarkEditTooltip", "বুকমার্ক এডিট করুন (Ctrl+D)", "Edit bookmark (Ctrl+D)"),
    ("BookmarkAddedTitle", "বুকমার্ক যোগ হয়েছে", "Bookmark added"),
    ("BookmarkEditTitle", "বুকমার্ক এডিট করুন", "Edit bookmark"),
    ("BookmarkNameLabel", "নাম", "Name"),
    ("BookmarkFolderLabel", "ফোল্ডার", "Folder"),
    ("BookmarkBarFolderName", "বুকমার্ক বার", "Bookmark bar"),
    ("BookmarkDone", "ঠিক আছে", "Done"),
    ("BookmarkRemove", "সরিয়ে দিন", "Remove"),
    ("BookmarkFolderEmpty", "(খালি)", "(Empty)"),
    ("BookmarkOpenInNewTab", "নতুন ট্যাবে খুলুন", "Open in new tab"),
    ("BookmarkRename", "নাম বদলান", "Rename"),
    ("BookmarkRenameTitle", "নতুন নাম", "New name"),
    ("BookmarkNewFolder", "নতুন ফোল্ডার", "New folder"),
    ("BookmarkNewFolderTitle", "ফোল্ডারের নাম", "Folder name"),
    ("BookmarkDelete", "মুছে ফেলুন", "Delete"),

    ("ImportFolderName", "ইমপোর্ট করা বুকমার্ক", "Imported bookmarks"),
    ("ImportDoneTitle", "বুকমার্ক ইমপোর্ট হয়েছে", "Bookmarks imported"),
    ("ImportDoneMessage", "{0}টি বুকমার্ক বুকমার্ক বারের নতুন ফোল্ডারে রাখা হয়েছে।", "{0} bookmarks were added to a new folder on the bookmark bar."),
    ("ImportSkippedMessage", "নিরাপত্তার জন্য {0}টি লিঙ্ক বাদ দেওয়া হয়েছে।", "{0} links were skipped for safety."),
    ("ImportFailedTitle", "বুকমার্ক ইমপোর্ট করা যায়নি", "Could not import bookmarks"),
    ("ImportTooLarge", "ফাইলটি অনেক বড়।", "The file is too large."),
    ("ImportReadError", "ফাইলটি পড়া যায়নি।", "The file could not be read."),
    ("ImportNothingFound", "এই ফাইলে কোনো বুকমার্ক পাওয়া যায়নি। Chrome বা Edge থেকে HTML ফাইল হিসেবে এক্সপোর্ট করা ফাইলটি বেছে নিন।", "No bookmarks were found in this file. Choose a file exported from Chrome or Edge as HTML."),

    ("TabCountFormat", "{0}টি ট্যাব খোলা", "{0} tabs open"),
    ("MenuSettings", "সেটিংস (Ctrl+,)", "Settings (Ctrl+,)"),
    ("SettingsTitle", "সেটিংস", "Settings"),
    ("ToggleOn", "চালু", "On"),
    ("ToggleOff", "বন্ধ", "Off"),

    ("DateTimeFormat", "{0} {1} {2}, {3}", "{0} {1} {2}, {3}"),
    ("TimeFormat", "{1} {0}", "{0} {1}"),
    ("TimeDawn", "ভোর", "AM"),
    ("TimeMorning", "সকাল", "AM"),
    ("TimeNoon", "দুপুর", "PM"),
    ("TimeAfternoon", "বিকাল", "PM"),
    ("TimeEvening", "সন্ধ্যা", "PM"),
    ("TimeNight", "রাত", "PM"),
    ("TimeAm", "পূর্বাহ্ণ", "AM"),
    ("TimePm", "অপরাহ্ণ", "PM"),
    ("Month1", "জানুয়ারি", "Jan"),
    ("Month2", "ফেব্রুয়ারি", "Feb"),
    ("Month3", "মার্চ", "Mar"),
    ("Month4", "এপ্রিল", "Apr"),
    ("Month5", "মে", "May"),
    ("Month6", "জুন", "Jun"),
    ("Month7", "জুলাই", "Jul"),
    ("Month8", "আগস্ট", "Aug"),
    ("Month9", "সেপ্টেম্বর", "Sep"),
    ("Month10", "অক্টোবর", "Oct"),
    ("Month11", "নভেম্বর", "Nov"),
    ("Month12", "ডিসেম্বর", "Dec"),

    ("SettingsGeneral", "প্রধান সেটিংস", "General"),
    ("SettingsSecurity", "নিরাপত্তা", "Security"),
    ("SettingsPrivacy", "প্রাইভেসি", "Privacy"),
    ("SettingsAppearance", "থিম", "Appearance"),
    ("SettingsAdvanced", "অ্যাডভান্সড", "Advanced"),

    ("SettingsLanguage", "ভাষা", "Language"),
    ("SettingsLanguageDescription", "মেনু, বাটন আর মেসেজ কোন ভাষায় দেখাবে।", "The language of menus, buttons and messages."),
    ("LanguageBangla", "বাংলা", "বাংলা"),
    ("LanguageEnglish", "English", "English"),
    ("SettingsLanguageRestartTitle", "রিস্টার্ট করতে হবে", "Restart needed"),
    ("SettingsLanguageRestartMessage", "নতুন ভাষা দেখতে অভিযাত্রী রিস্টার্ট করুন। খোলা ট্যাবগুলো আবার ফিরে আসবে।", "Restart Obhijatri to see the new language. Your open tabs will come back."),
    ("SettingsRestartNow", "এখনই রিস্টার্ট করুন", "Restart now"),
    ("SettingsHomePage", "হোম পেজ", "Home page"),
    ("SettingsHomePageDescription", "নতুন ট্যাবে আর হোম বাটনে চাপলে এই পেজটি খুলবে।", "This page opens in new tabs and when you press Home."),
    ("SettingsHomePageSaved", "সেভ হয়েছে।", "Saved."),
    ("SettingsHomePageInvalid", "এটি সঠিক ওয়েবসাইটের ঠিকানা নয়। যেমন: prothomalo.com", "This is not a valid web address. For example: prothomalo.com"),
    ("SettingsSearchEngine", "সার্চ ইঞ্জিন", "Search engine"),
    ("SettingsSearchEngineDescription", "অ্যাড্রেস বারে কিছু লিখে সার্চ করলে কোথায় খোঁজা হবে।", "Where words typed in the address bar are searched."),
    ("SearchEngineGoogle", "Google", "Google"),
    ("SearchEngineBing", "Bing", "Bing"),
    ("SearchEngineDuckDuckGo", "DuckDuckGo", "DuckDuckGo"),
    ("SettingsRestoreTabs", "আগের ট্যাবগুলো আবার খুলুন", "Reopen previous tabs"),
    ("SettingsRestoreTabsDescription", "অভিযাত্রী চালু হলে গতবারের খোলা ট্যাবগুলো ফিরে আসবে।", "When Obhijatri starts, the tabs from last time come back."),

    ("SettingsSmartScreen", "Microsoft Defender SmartScreen", "Microsoft Defender SmartScreen"),
    ("SettingsSmartScreenDescription", "ক্ষতিকর ওয়েবসাইট আর ডাউনলোড সম্পর্কে সতর্ক করে। চালু রাখাই ভালো।", "Warns about harmful sites and downloads. Keeping it on is recommended."),
    ("SettingsPopups", "পপ-আপ ব্লক", "Pop-up blocking"),
    ("SettingsPopupsDescription", "আপনি নিজে ক্লিক না করলে কোনো সাইট নতুন উইন্ডো বা ট্যাব খুলতে পারে না। এটি সবসময় চালু থাকে।", "Sites cannot open new windows or tabs unless you clicked. This is always on."),
    ("SettingsSafeSchemes", "নিরাপদ ঠিকানা", "Safe addresses"),
    ("SettingsSafeSchemesDescription", "কোনো ওয়েবসাইট আপনার কম্পিউটারের ফাইল বা অন্য প্রোগ্রাম খুলতে পারে না। এটি সবসময় চালু থাকে।", "Web pages cannot open files or other programs on your computer. This is always on."),

    ("SettingsTracking", "ট্র্যাকিং প্রোটেকশন", "Tracking protection"),
    ("SettingsTrackingDescription", "যারা এক সাইট থেকে আরেক সাইটে আপনাকে ট্র্যাক করে, তাদের আটকায়।", "Blocks trackers that follow you from site to site."),
    ("SettingsTrackingBasic", "বেসিক: বেশিরভাগ ট্র্যাকার চলবে, সব সাইট ঠিকমতো কাজ করবে", "Basic: most trackers allowed, all sites work"),
    ("SettingsTrackingBalanced", "ব্যালান্সড: অচেনা সাইটের ট্র্যাকার আটকাবে (এটাই ভালো)", "Balanced: blocks trackers from sites you have not visited (recommended)"),
    ("SettingsTrackingStrict", "স্ট্রিক্ট: প্রায় সব ট্র্যাকার আটকাবে, কিছু সাইট ঠিকমতো না চলতে পারে", "Strict: blocks most trackers, some sites may not work"),
    ("SettingsClearSiteData", "কুকি ও সাইটের ডেটা", "Cookies and site data"),
    ("SettingsClearSiteDataDescription", "সব সাইট থেকে লগ আউট হয়ে যাবেন এবং ক্যাশ ফাইল মুছে যাবে।", "You will be signed out of all sites and cached files will be removed."),
    ("SettingsClearSiteDataButton", "কুকি ও সাইটের ডেটা মুছুন", "Clear cookies and site data"),
    ("SettingsClearSiteDataConfirmTitle", "কুকি ও সাইটের ডেটা মুছবেন?", "Clear cookies and site data?"),
    ("SettingsClearSiteDataConfirmMessage", "সব সাইট থেকে লগ আউট হয়ে যাবেন। এটি আর ফেরানো যাবে না।", "You will be signed out of all sites. This cannot be undone."),
    ("SettingsClearSiteDataDone", "মুছে ফেলা হয়েছে।", "Cleared."),
    ("SettingsClearSiteDataNothing", "এখনো কোনো ওয়েবসাইট খোলা হয়নি, তাই মোছার কিছু নেই।", "No web page has been opened yet, so there is nothing to clear."),
    ("SettingsHistory", "হিস্ট্রি", "History"),
    ("SettingsHistoryDescription", "কোন কোন পেজ দেখেছেন তার তালিকা দেখুন বা মুছুন। প্রাইভেট উইন্ডোর পেজ কখনো হিস্ট্রিতে থাকে না।", "See or clear the list of pages you visited. Private window pages are never kept."),
    ("SettingsOpenHistory", "হিস্ট্রি খুলুন", "Open history"),

    ("SettingsTheme", "লাইট ও ডার্ক মোড", "Theme"),
    ("SettingsThemeDescription", "অভিযাত্রী আর ওয়েবসাইট লাইট না ডার্ক মোডে দেখাবে।", "Whether Obhijatri and web pages use light or dark colours."),
    ("SettingsThemeSystem", "Windows-এর মতো", "Same as Windows"),
    ("SettingsThemeLight", "লাইট", "Light"),
    ("SettingsThemeDark", "ডার্ক", "Dark"),
    ("SettingsBookmarkBar", "বুকমার্ক বার দেখান", "Show bookmark bar"),
    ("SettingsBookmarkBarDescription", "অ্যাড্রেস বারের নিচে পছন্দের পেজগুলো দেখাবে।", "Shows your favourite pages under the address bar."),
    ("SettingsVerticalTabs", "ট্যাব পাশে দেখান", "Show tabs on the side"),
    ("SettingsVerticalTabsDescription", "অনেক ট্যাব খোলা থাকলে পাশে লম্বা লিস্টে নাম পড়া সহজ হয়।", "With many tabs open, a list on the side is easier to read."),
    ("SettingsFont", "বাংলা ফন্ট", "Bangla font"),
    ("SettingsFontDescription", "অভিযাত্রীর মেনু আর বাটনে Hind Siliguri ফন্ট ব্যবহার হয়, যা অ্যাপের সঙ্গেই আছে।", "Obhijatri's menus and buttons use the bundled Hind Siliguri font."),

    ("SettingsDataFolder", "ডেটা ফোল্ডার", "Data folder"),
    ("SettingsDataFolderDescription", "হিস্ট্রি, বুকমার্ক আর সেটিংস শুধু এই কম্পিউটারের এই ফোল্ডারে থাকে। কোথাও পাঠানো হয় না।", "History, bookmarks and settings are kept only in this folder on this computer. Nothing is sent anywhere."),
    ("SettingsOpenDataFolder", "ফোল্ডার খুলুন", "Open folder"),
    ("SettingsVersion", "ভার্সন", "Version"),
]

keys = [r[0] for r in rows]
assert len(keys) == len(set(keys)), "duplicate keys"
for r in rows:
    for text in r[1:]:
        assert "\u2014" not in text and "\u2013" not in text, f"dash in {r[0]}"

head = """<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
"""
for i, folder in ((1, "bn-BD"), (2, "en-US")):
    body = "".join(
        f'  <data name="{r[0]}" xml:space="preserve"><value>{html.escape(r[i], quote=False)}</value></data>\n'
        for r in rows)
    with open(os.path.join(ROOT, folder, "Resources.resw"), "w", encoding="utf-8", newline="\r\n") as f:
        f.write(head + body + "</root>\n")
print(len(rows), "strings written")
