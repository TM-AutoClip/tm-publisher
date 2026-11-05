"""Writes docs/platforms/<platform>.md (English) and <platform>.tr.md (Turkish) from the app's own guide texts.

The steps and warnings come from src/TMPublisher.Desktop/Lang/*.json and from each platform's Guide definition, so the
documentation says exactly what the connection window says. Run it after changing a guide:

    python tools/gen-platform-docs.py
"""
import json
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LANG = os.path.join(ROOT, "src", "TMPublisher.Desktop", "Lang")
PLATFORMS = os.path.join(ROOT, "src", "TMPublisher.Platforms")
OUT = os.path.join(ROOT, "docs", "platforms")

# code: (folder, class prefix, display name, return address or None, what can be posted (en, tr))
INFO = {
    "youtube": ("YouTube", "YouTube", "YouTube", "http://127.0.0.1:47811/callback",
                ("One video per post (regular or Shorts) with title, description and tags. Uploads start private unless you choose another visibility.",
                 "Gönderi başına bir video (normal ya da Shorts): başlık, açıklama ve etiketlerle. Başka görünürlük seçmezseniz gizli yüklenir.")),
    "x": ("X", "X", "X", "http://127.0.0.1:47812/callback",
          ("Text (280 weighted characters), up to 4 photos or one video. X charges per post: about $0.015, $0.20 with a link.",
           "Metin (280 ağırlıklı karakter), en çok 4 fotoğraf ya da bir video. X gönderi başına ücret alır: yaklaşık 0,015 $, bağlantılı 0,20 $.")),
    "tiktok": ("TikTok", "TikTok", "TikTok", "http://127.0.0.1:47813/callback",
               ("One video per post. By default it goes to your TikTok inbox as a draft; an optional direct post is always private and needs a private TikTok account.",
                "Gönderi başına bir video. Varsayılan olarak TikTok gelen kutunuza taslak gider; isteğe bağlı doğrudan paylaşım her zaman gizlidir ve gizli bir TikTok hesabı ister.")),
    "instagram": ("Meta", "Instagram", "Instagram", None,
                  ("Videos (Reels) only in this version, caption up to 2,200 characters and 5 hashtags.",
                   "Bu sürümde yalnız video (Reels); açıklama en çok 2.200 karakter ve 5 etiket.")),
    "facebook": ("Meta", "Facebook", "Facebook Page", None,
                 ("Text, links, photos and one video on a Facebook Page you manage.",
                  "Yönettiğiniz bir Facebook Sayfasında metin, bağlantı, fotoğraf ve bir video.")),
    "threads": ("Threads", "Threads", "Threads", "https://localhost/callback",
                ("Text only in this version (500 characters).",
                 "Bu sürümde yalnız metin (500 karakter).")),
    "bluesky": ("Bluesky", "Bluesky", "Bluesky", None,
                ("Text (300 characters), up to 4 photos or one video.",
                 "Metin (300 karakter), en çok 4 fotoğraf ya da bir video.")),
    "mastodon": ("Mastodon", "Mastodon", "Mastodon", None,
                 ("Text (500 characters on most servers), up to 4 photos or one video, with a visibility choice.",
                  "Metin (çoğu sunucuda 500 karakter), en çok 4 fotoğraf ya da bir video; görünürlük seçilebilir.")),
    "telegram": ("Telegram", "Telegram", "Telegram", None,
                 ("Text, photos, videos and albums to a channel or group through your own bot.",
                  "Kendi botunuzla bir kanala ya da gruba metin, fotoğraf, video ve albüm.")),
    "discord": ("Discord", "Discord", "Discord", None,
                ("Text and files to a channel through a webhook.",
                 "Webhook ile bir kanala metin ve dosya.")),
    "pinterest": ("Pinterest", "Pinterest", "Pinterest", "https://localhost/callback",
                  ("One image or one video pin with title, description and an optional link, to the board you choose.",
                   "Seçtiğiniz panoya başlık, açıklama ve isteğe bağlı bağlantıyla bir resim ya da bir video pini.")),
    "linkedin": ("LinkedIn", "LinkedIn", "LinkedIn", None,
                 ("Text, photos or one video on your personal profile.",
                  "Kişisel profilinizde metin, fotoğraf ya da bir video.")),
}

TEXT = {
    "en": {
        "other": "[Türkçe](%s.tr.md)",
        "back": "[← Back to the README](../../README.md#connect-your-accounts)",
        "post": "What you can post",
        "address": "Return address",
        "address_text": "Register exactly this address in your app (the connection window shows it with a copy button):",
        "steps": "Steps",
        "notes": "Good to know",
        "own": "TM Publisher talks to %s only with **your own** developer app or key. Nothing goes through a server of ours; there is none.",
        "end": "When the test succeeds the account appears under **Accounts** and can be used in posts and groups.",
    },
    "tr": {
        "other": "[English](%s.md)",
        "back": "[← README'ye dön](../../README.tr.md#hesaplarınızı-bağlayın)",
        "post": "Neler gönderilebilir",
        "address": "Dönüş adresi",
        "address_text": "Uygulamanıza tam olarak bu adresi kaydedin (bağlantı penceresi kopyalama düğmesiyle gösterir):",
        "steps": "Adımlar",
        "notes": "Bilmeniz gerekenler",
        "own": "TM Publisher %s ile yalnız **sizin kendi** geliştirici uygulamanız ya da anahtarınızla konuşur. Hiçbir şey bizim bir sunucumuzdan geçmez; öyle bir sunucu yok.",
        "end": "Deneme başarılı olunca hesap **Hesaplar** altında görünür; gönderilerde ve gruplarda kullanılabilir.",
    },
}


def guide(folder, prefix):
    source = open(os.path.join(PLATFORMS, folder, "Concrete", prefix + "Platform.cs"), encoding="utf-8").read()
    match = re.search(r"Steps\((\d+),(.*?)\);", source, re.S)
    count = int(match.group(1))
    links = {int(n): url for n, url in re.findall(r'\[(\d+)\]\s*=\s*new\("([^"]+)"\)', match.group(2))}
    warnings = re.findall(r'"(warning\.[a-z_.]+)"', match.group(2))
    return count, links, warnings


def write(code, lang, strings):
    folder, prefix, name, address, post = INFO[code]
    count, links, warnings = guide(folder, prefix)
    t = TEXT[lang]
    lines = [f"# {name}", "", t["other"] % code + " · " + t["back"], "", t["own"] % name, "", f"## {t['post']}", "",
             post[0 if lang == "en" else 1], ""]
    if address:
        lines += [f"## {t['address']}", "", t["address_text"], "", "```", address, "```", ""]
    lines += [f"## {t['steps']}", ""]
    for i in range(1, count + 1):
        step = strings[f"guide.{code}.{i}"]
        if i in links:
            step += f" <{links[i]}>"
        lines.append(f"{i}. {step}")
    lines += ["", t["end"], ""]
    if warnings:
        lines += [f"## {t['notes']}", ""]
        lines += [f"- {strings[w]}" for w in warnings]
        lines.append("")
    suffix = ".md" if lang == "en" else ".tr.md"
    with open(os.path.join(OUT, code + suffix), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))


def main():
    os.makedirs(OUT, exist_ok=True)
    for lang in ("en", "tr"):
        strings = json.load(open(os.path.join(LANG, lang + ".json"), encoding="utf-8"))
        for code in INFO:
            write(code, lang, strings)
    print(f"{len(INFO) * 2} files in docs/platforms")


if __name__ == "__main__":
    main()
