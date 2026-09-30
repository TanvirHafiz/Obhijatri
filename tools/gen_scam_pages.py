"""
Writes tests/Fixtures/scam-pages.json: the page signals (address, title, visible text, what the forms
ask for) of made-up scam pages and of ordinary pages, for the rule based scorer's tests. Every
address is invented or belongs to a real site's public front page; no real scam site is visited.

    python tools/gen_scam_pages.py
"""
import json
import os

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")


def form(pw=False, otp=False, card=False, phone=False, nid=False, action=None):
    return {"actionHost": action, "password": pw, "otp": otp, "card": card, "phone": phone, "id": nid}


def page(name, url, title, text, forms=(), countdown=False, contact=False):
    return {"name": name, "url": url, "title": title, "text": text, "forms": list(forms), "countdown": countdown, "contactLink": contact}


scam = [
    # Lookalike addresses (the shield flags these on its own): red.
    page("bkash lookalike login", "https://bkash-verify.xyz/login", "bKash Login",
         "আপনার বিকাশ অ্যাকাউন্ট ভেরিফাই করুন। এখনই ভেরিফাই না করলে অ্যাকাউন্ট বন্ধ হয়ে যাবে। আপনার পিন নম্বর দিন।",
         [form(pw=True, otp=True)]),
    page("nagad lookalike", "https://nagad-bd.xyz/", "নগদ অফার",
         "নগদ ব্যবহারকারীদের জন্য বিশেষ বোনাস। আপনার নগদ নম্বর ও পিন দিন।", [form(otp=True, phone=True)]),
    page("facebook typo", "https://faceboook.com/login", "Facebook",
         "Log in to Facebook. Your account will be suspended within 24 hours unless you verify.", [form(pw=True)]),
    page("paypal subdomain trick", "https://paypal.com.verify-user.xyz/", "PayPal Security",
         "Confirm your account. Your account has been locked. Verify your identity now.", [form(pw=True, card=True)]),
    page("daraz offer", "https://daraz-offer.xyz/", "Daraz মেগা অফার",
         "আপনি বিজয়ী হয়েছেন! ফ্রি রিচার্জ পেতে আপনার নম্বর দিন।", [form(phone=True)], countdown=True),
    page("gp recharge", "https://grameenphone-recharge.info/", "GP ফ্রি রিচার্জ",
         "গ্রামীণফোন গ্রাহকদের জন্য ফ্রি রিচার্জ। সীমিত সময়ের মধ্যে ক্লিক করুন।", [form(phone=True)], countdown=True),

    # Unknown addresses that copy a brand and ask for a password, PIN or OTP.
    page("fake bkash on random host", "https://secure-pay-bd.top/bkash", "bKash অ্যাকাউন্ট আপডেট",
         "বিকাশ অ্যাকাউন্ট আপডেট করুন। এখনই আপডেট না করলে অ্যাকাউন্ট ব্লক হয়ে যাবে। আপনার বিকাশ পিন দিন।",
         [form(pw=True, otp=True, action="collect-data.top")]),
    page("fake bank login", "https://ebl-netbanking.click/", "Eastern Bank internet banking",
         "Enter your user name, password and OTP. Your account will be blocked within 24 hours.", [form(pw=True, otp=True)]),
    page("fake nagad pin page", "https://claim-nagad.icu/", "Nagad Bonus",
         "Nagad bonus is waiting for you. Enter your PIN to receive. Nagad, Nagad, Nagad.", [form(otp=True, phone=True)]),
    page("fake facebook security", "https://fb-security-help.buzz/", "Facebook Security Check",
         "Facebook Security. Verify your account now or it will be permanently disabled. Facebook team.",
         [form(pw=True, action="fb-security-help.buzz")]),
    page("fake google drive", "https://drive-share-docs.xyz/view", "Google Drive: shared document",
         "Sign in with Google to view the document. Verify your account. Act now.", [form(pw=True)]),
    page("fake rocket", "http://rocket-dbbl-update.info/", "Rocket KYC",
         "রকেট কেওয়াইসি আপডেট করুন। আপনার রকেট পিন দিন। এখনই আপডেট না করলে অ্যাকাউন্ট বন্ধ হয়ে যাবে।",
         [form(otp=True, nid=True)]),

    # Prize and lottery.
    page("lucky draw", "https://lucky-winner-bd.top/", "লাকি ড্র বিজয়ী",
         "অভিনন্দন! আপনি বিজয়ী হয়েছেন। লাকি ড্র থেকে ৫০,০০০ টাকা পুরস্কার জিতেছেন। পুরস্কার পেতে নম্বর দিন।",
         [form(phone=True)], countdown=True),
    page("iphone giveaway", "https://free-iphone-2026.click/", "Claim your free iPhone",
         "Congratulations! You have won a free iPhone. Claim your prize now, only 3 left. Lucky winner selected.",
         [form(phone=True, card=True)], countdown=True),
    page("gift card", "https://gift-cards-daily.xyz/", "Free gift card",
         "আপনি জিতেছেন একটি গিফট কার্ড। ফ্রি টাকা পেতে এখনই ক্লিক করুন। লটারিতে আপনার নাম উঠেছে।", [form(phone=True)]),
    page("cash bonus", "https://bonus-cash-bd.buzz/", "বোনাস পেতে ক্লিক করুন",
         "বোনাস পেতে ক্লিক করুন। ফ্রি টাকা ও বিনামূল্যে রিচার্জ। শেষ সুযোগ, আজই শেষ।", [form(phone=True)]),
    page("prize sms link", "http://sms-prize-claim.top/x", "Prize",
         "You are the winner. Claim your reward. Free money waiting.", [form(phone=True)]),

    # SIM, KYC, NID threats.
    page("sim block", "https://sim-verify-bd.info/", "সিম রেজিস্ট্রেশন যাচাই",
         "আপনার সিম বন্ধ হয়ে যাবে। এনআইডি ভেরিফিকেশন সম্পন্ন করুন। ২৪ ঘণ্টার মধ্যে যাচাই করুন।", [form(nid=True, phone=True)]),
    page("nid update", "https://nid-update-bd.xyz/", "জাতীয় পরিচয়পত্র আপডেট",
         "জাতীয় পরিচয়পত্র আপডেট করুন। এনআইডি যাচাই না করলে মোবাইল নম্বর বন্ধ হয়ে যাবে।", [form(nid=True)]),
    page("kyc english", "https://mobile-kyc-update.click/", "KYC verification",
         "Complete your KYC. Your SIM will be blocked. KYC update required within 24 hours.", [form(nid=True, phone=True)]),
    page("passport fee", "https://epassport-fast-track.top/", "ই-পাসপোর্ট ফাস্ট ট্র্যাক",
         "পাসপোর্ট দ্রুত পেতে রেজিস্ট্রেশন ফি দিন। এনআইডি যাচাই করুন। শেষ সুযোগ।", [form(nid=True, phone=True, card=True)]),

    # Jobs and investment.
    page("work from home", "https://easy-income-bd.xyz/", "ঘরে বসে আয় করুন",
         "ঘরে বসে আয় করুন দৈনিক ৫০০০ টাকা। নিশ্চিত লাভ। রেজিস্ট্রেশন ফি ৫০০ টাকা অগ্রিম দিন।", [form(phone=True)]),
    page("double money", "https://invest-double.top/", "টাকা দ্বিগুণ",
         "বিনিয়োগ করলে দ্বিগুণ টাকা। গ্যারান্টিড প্রফিট। guaranteed profit, double your money.", [form(phone=True, card=True)]),
    page("fake job offer", "https://gulf-jobs-visa.click/", "বিদেশে চাকরি",
         "বিদেশে চাকরি নিশ্চিত। ভিসা ফি অগ্রিম টাকা দিন। রেজিস্ট্রেশন ফি লাগবে। চাকরি নিশ্চিত।", [form(phone=True, nid=True)], countdown=True),
    page("online earning", "https://earn-per-day.icu/", "Earn per day",
         "Earn per day from home. Work from home and earn 5000 taka. Pay a small fee to receive your account.", [form(phone=True)]),

    # OTP / PIN harvesting with pressure.
    page("otp verify countdown", "https://verify-otp-now.xyz/", "OTP যাচাই",
         "আপনার ওটিপি দিন। ওটিপি কোড দিন নাহলে অ্যাকাউন্ট ব্লক হয়ে যাবে।", [form(otp=True)], countdown=True),
    page("pin update", "https://pin-secure-update.click/", "PIN Update",
         "Enter your PIN and OTP to keep your account. Your account will be suspended. Act now.", [form(otp=True, pw=True)], countdown=True),
    page("bank card verify", "https://card-verify-bd.buzz/", "কার্ড ভেরিফিকেশন",
         "আপনার কার্ড ভেরিফাই করুন। এখনই ভেরিফাই না করলে অ্যাকাউন্ট ব্লক হয়ে যাবে। কার্ড নম্বর ও ওটিপি দিন।",
         [form(card=True, otp=True)]),
    page("wallet unlock", "https://wallet-unlock-help.top/", "Wallet unlocked",
         "Your account has been locked. Verify your identity, enter your OTP within 24 hours.", [form(otp=True, pw=True)]),

    # No form at all: the page asks people to message a number or channel instead.
    page("whatsapp prize", "https://prize-whatsapp-bd.click/", "লাকি ড্র বিজয়ী",
         "অভিনন্দন! আপনি বিজয়ী হয়েছেন। পুরস্কার পেতে এখনই হোয়াটসঅ্যাপে মেসেজ করুন। আপনি জিতেছেন ফ্রি টাকা।", contact=True),
    page("telegram job", "https://remote-jobs-easy.top/", "ঘরে বসে আয়",
         "ঘরে বসে আয় করুন। দৈনিক আয় ৩০০০ টাকা। রেজিস্ট্রেশন ফি লাগবে। টেলিগ্রামে যোগাযোগ করুন।", contact=True),

    # English phishing.
    page("netflix billing", "https://netflix-billing-update.xyz/", "Netflix: update payment",
         "Your Netflix payment failed. Your account will be closed. Update your payment method now to keep watching Netflix.",
         [form(card=True, pw=True)]),
    page("microsoft support", "https://windows-alert-support.click/", "Microsoft Support Alert",
         "Warning: virus detected. Call Microsoft now. Your account has been suspended. Immediate action required.", [form(phone=True)], countdown=True),
    page("paypal refund", "https://paypal-refund-center.buzz/", "PayPal refund",
         "You are eligible for a PayPal refund. Confirm your account, enter your password and card details within 12 hours.",
         [form(pw=True, card=True)]),
]

clean = [
    # News, including stories about scams that quote scam phrases.
    page("prothom alo article", "https://www.prothomalo.com/bangladesh/abc123", "রাজধানীতে যানজট কমাতে নতুন সেতু",
         "রাজধানীর যানজট কমাতে নতুন সেতুটি আগামী সপ্তাহে খুলে দেওয়া হবে। কর্তৃপক্ষ জানিয়েছে, প্রকল্পটি তিন বছরে শেষ হয়েছে।"),
    page("daily star article", "https://www.thedailystar.net/news/economy/xyz", "Exports rise in September",
         "Bangladesh's export earnings rose in September, according to the Export Promotion Bureau. Officials said the growth came mainly from garments."),
    page("bdnews24 sports", "https://bdnews24.com/sports/123", "বাংলাদেশ জিতেছে",
         "শেষ ওভারে নাটকীয় জয়ে সিরিজ জিতেছে বাংলাদেশ। অধিনায়ক বলেছেন, দলের জন্য এটি বড় অর্জন। জিতেছে টাইগাররা।"),
    page("kaler kantho scam warning", "https://www.kalerkantho.com/online/national/1", "মোবাইল ব্যাংকিংয়ে প্রতারণা বাড়ছে",
         "প্রতারকরা ফোন করে বলে আপনি লটারি জিতেছেন। কেউ বলে সিম বন্ধ হয়ে যাবে। কেউ বলে অ্যাকাউন্ট বন্ধ হয়ে যাবে। "
         "বিটিআরসি বলেছে, এ ধরনের ফোনে কেউ পিন বা ওটিপি দেবেন না। বিকাশ ও নগদের কর্মকর্তারাও একই পরামর্শ দিয়েছেন।"),
    page("jugantor scam story", "https://www.jugantor.com/national/2", "ভুয়া এনআইডি ভেরিফিকেশনের ফাঁদ",
         "এনআইডি ভেরিফিকেশনের নামে প্রতারণা। প্রতারকরা বলে ২৪ ঘণ্টার মধ্যে যাচাই না করলে নম্বর বন্ধ হয়ে যাবে। "
         "পুলিশ সতর্ক থাকতে বলেছে। সাইবার ক্রাইম ইউনিট এ নিয়ে তদন্ত করছে।"),
    page("english scam explainer", "https://www.dhakatribune.com/bangladesh/3", "How to spot online scams",
         "Scammers say you have won a lottery or that your account will be suspended within 24 hours. Never share your OTP or PIN. "
         "Banks and mobile financial services say they never ask for it by phone. Learn how to report scams."),
    page("wikipedia article", "https://bn.wikipedia.org/wiki/বাংলাদেশ", "বাংলাদেশ - উইকিপিডিয়া",
         "বাংলাদেশ দক্ষিণ এশিয়ার একটি সার্বভৌম রাষ্ট্র। এর রাজধানী ঢাকা। ১৯৭১ সালের মুক্তিযুদ্ধের মাধ্যমে দেশটি স্বাধীন হয়।"),
    page("recipe blog", "https://www.example-food.com/recipes/khichuri", "ইলিশ খিচুড়ি রেসিপি",
         "বৃষ্টির দিনে ইলিশ খিচুড়ি। চাল, ডাল, ইলিশ মাছ ও মশলা লাগবে। ঘরে বসে সহজে রান্না করুন।"),

    # Official sites of protected brands (login, PIN and OTP forms are normal there).
    page("bkash official login", "https://www.bkash.com/login", "bKash Login",
         "বিকাশ অ্যাকাউন্টে লগইন করুন। আপনার পিন দিন। ওটিপি দিন। বিকাশ বিকাশ বিকাশ।", [form(otp=True, phone=True, action="www.bkash.com")]),
    page("facebook official login", "https://www.facebook.com/login", "Facebook - log in",
         "Log in to Facebook. Enter your password. Verify your account if asked.", [form(pw=True, action="www.facebook.com")]),
    page("google sign in", "https://accounts.google.com/signin", "Sign in - Google Accounts",
         "Sign in with your Google Account. Enter your password. Verify your identity.", [form(pw=True, otp=True, action="accounts.google.com")]),
    page("ebl netbanking", "https://www.ebl.com.bd/login", "EBL Internet Banking",
         "ইন্টারনেট ব্যাংকিং লগইন। ইউজার নেম ও পাসওয়ার্ড দিন। ওটিপি দিন।", [form(pw=True, otp=True, action="www.ebl.com.bd")]),
    page("daraz home", "https://www.daraz.com.bd/", "Daraz Bangladesh: Online Shopping",
         "Flash sale ends soon. ফ্রি ডেলিভারি। limited time offer. বিকাশে পেমেন্ট করলে ক্যাশব্যাক।", countdown=True),
    page("gov portal", "https://bangladesh.gov.bd/", "বাংলাদেশ জাতীয় তথ্য বাতায়ন",
         "সরকারি সেবা, তথ্য ও ফরম। জাতীয় পরিচয়পত্র সংশোধন ও যাচাই সংক্রান্ত তথ্য।", [form(nid=True)]),
    page("paypal official", "https://www.paypal.com/signin", "PayPal: Log in",
         "Log in to your PayPal account. Enter your password.", [form(pw=True, action="www.paypal.com")]),
    page("grameenphone official", "https://www.grameenphone.com/", "Grameenphone",
         "গ্রামীণফোন প্যাকেজ, রিচার্জ ও অফার। সীমিত সময়ের মধ্যে অফার। MyGP অ্যাপ ডাউনলোড করুন।", [form(phone=True)]),

    # Ordinary sites that use forms, timers or brand names.
    page("shop flash sale", "https://www.chaldal.com/", "Chaldal: online grocery",
         "Flash sale ends in 02:15:33. সীমিত সময়ের অফার। Order groceries and get them delivered.", [form(phone=True)], countdown=True),
    page("shop checkout with bkash", "https://www.rokomari.com/checkout", "Rokomari checkout",
         "Payment methods: bKash, Nagad, Rocket, cards. Pay with bKash for a discount. bKash is the fastest. Nagad also accepted. "
         "Enter your card number, expiry and CVV.", [form(card=True, phone=True, action="www.rokomari.com")]),
    page("shop login lists bkash", "https://www.pickaboo.com/login", "Pickaboo login",
         "Log in with your password. We accept bKash, bKash payment, bKash cashback, Nagad and cards.", [form(pw=True)]),
    page("ride app otp", "https://www.pathao.com/login", "Pathao: log in",
         "Enter your mobile number. We will send an OTP. Enter the OTP to continue.", [form(otp=True, phone=True)]),
    page("university admission", "https://admission.example.ac.bd/apply", "Admission 2026",
         "Apply online. Last date of application is 15 October. Enter your SSC roll and registration number.", [form(nid=True, phone=True)]),
    page("job portal", "https://jobs.bdjobs.com/search", "Find jobs in Bangladesh",
         "Search jobs by category. Software engineer, accountant, marketing executive. Upload your CV to apply.", [form(pw=True)]),
    page("company contact form", "https://www.acme-textiles.com.bd/contact", "Contact Acme Textiles",
         "Send us a message. Our phone number and address. We reply within two working days.", [form(phone=True)]),
    page("newsletter signup", "https://www.example-blog.com/", "A weekly blog about Dhaka",
         "Subscribe to the newsletter. Posts about food, travel and books.", [form(action="feedburner.google.com")]),
    page("local file host page", "http://localhost:8080/test", "Local test",
         "Sign in to the test page. Enter password and OTP for the demo.", [form(pw=True, otp=True)]),
    page("election results", "https://www.ecs.gov.bd/results", "নির্বাচনের ফলাফল",
         "ফলাফল দেখুন। আসন অনুযায়ী বিজয়ী প্রার্থীদের তালিকা। অভিনন্দন জানিয়েছেন প্রধান নির্বাচন কমিশনার।"),
    page("cricket live", "https://www.espncricinfo.com/live", "Live cricket score",
         "Bangladesh won by 5 wickets. Congratulations to the team. Watch the highlights.", countdown=True),
    page("bank branch locator", "https://www.dutchbanglabank.com/branches", "Branch locator",
         "Find a branch or ATM near you. Search by district.", [form(phone=True)]),
    page("telecom support article", "https://www.banglalink.net/en/support", "Banglalink support",
         "Manage your SIM and package. Visit any customer care centre with your NID for SIM replacement.", [form(nid=True, phone=True)]),
    page("online course", "https://www.10minuteschool.com/courses", "Online courses",
         "Learn English, IELTS and skills at home. Enroll now, seats are limited. Certificate on completion.", [form(phone=True)]),
    page("youtube watch", "https://www.youtube.com/watch?v=abc", "Bangla song - YouTube", "গান শুনুন। সাবস্ক্রাইব করুন। লাইক দিন।"),
    page("weather page", "https://www.accuweather.com/dhaka", "Dhaka weather", "আজ ঢাকায় আংশিক মেঘলা। তাপমাত্রা ৩২ ডিগ্রি সেলসিয়াস।"),
]

# Guard the data itself: every entry has a distinct name.
names = [p["name"] for p in scam + clean]
assert len(names) == len(set(names)), "duplicate names"
assert len(scam) >= 30 and len(clean) >= 30, (len(scam), len(clean))

out = {
    "source": "Made-up pages written for Obhijatri's tests (tools/gen_scam_pages.py); addresses are invented or real front pages. Do not edit by hand.",
    "scam": scam,
    "clean": clean,
}
path = os.path.join(ROOT, "tests", "Fixtures", "scam-pages.json")
with open(path, "w", encoding="utf-8") as f:
    json.dump(out, f, ensure_ascii=False, indent=1)
print(len(scam), "scam pages,", len(clean), "clean pages written")
