# تنصيب HashiCorp Vault — بيئة الإنتاج (Contabo VPS)

> **هذا الدليل لبيئة الإنتاج الحقيقية على الـ VPS.** بيئة التطوير المحلية بتستخدم `vault server -dev` (وضع تطوير بسيط، تخزين في الذاكرة، Unseal تلقائي) — تفاصيله في نهاية هذا الملف، قسم "وضع التطوير المحلي".
>
> مرجعي: `Docs/Implementation/HR-Core-Plan.md §0.6` (القرار: Vault كامل، لا حل انتقالي دائم) وملحق د.1 في `Docs/Modules/10-Module-HR-Payroll.md`.

---

## 1. المتطلبات المسبقة

- وصول SSH لسيرفر Contabo VPS (نفس السيرفر اللي مستضاف عليه التطبيق، أو سيرفر منفصل — التوصية: **منفصل** لو ممكن، عشان فقدان سيرفر التطبيق ما يفقدش Vault في نفس اللحظة).
- بورت **8200** (API) و**8201** (Cluster، اختياري لو Vault واحد بس) متاحين على الشبكة الداخلية، **مقفولين من برّه** (Firewall — الوصول لـ Vault يكون بس من سيرفر التطبيق نفسه).
- OpenSSL (لتوليد الشهادة الذاتية).
- صلاحية `root`/`sudo` على السيرفر.

---

## 2. تنزيل وتنصيب Vault

```bash
# على السيرفر (Linux amd64) — تأكد من رقم الإصدار الأحدث على releases.hashicorp.com/vault
VAULT_VERSION="2.1.1"
curl -O "https://releases.hashicorp.com/vault/${VAULT_VERSION}/vault_${VAULT_VERSION}_linux_amd64.zip"
curl -O "https://releases.hashicorp.com/vault/${VAULT_VERSION}/vault_${VAULT_VERSION}_SHA256SUMS"

# تحقق من الـ Checksum قبل أي حاجة تانية — إلزامي
sha256sum --check --ignore-missing "vault_${VAULT_VERSION}_SHA256SUMS"

sudo unzip "vault_${VAULT_VERSION}_linux_amd64.zip" -d /usr/local/bin
sudo chown root:root /usr/local/bin/vault
sudo chmod 755 /usr/local/bin/vault

sudo useradd --system --home /etc/vault.d --shell /bin/false vault
sudo mkdir -p /opt/vault/data /etc/vault.d /etc/vault.d/tls
sudo chown -R vault:vault /opt/vault /etc/vault.d
```

---

## 3. شهادة TLS ذاتية التوقيع

مقبولة لـ Dev/Staging وللإصدار الأول من الإنتاج على VPS ذاتي الإدارة (مفيش Load Balancer مُدار بيوفر شهادة تلقائيًا). **إذا توفرت شهادة حقيقية (Let's Encrypt مثلًا) لاحقًا، تستبدل هنا من غير أي تغيير في كود التطبيق** (الكود بيقرا `Vault:SkipTlsVerify` من الإعداد، مش بيفترض نوع الشهادة).

```bash
cd /etc/vault.d/tls
sudo openssl req -x509 -newkey rsa:4096 -sha256 -days 825 -nodes \
  -keyout vault-key.pem -out vault-cert.pem \
  -subj "/CN=vault" \
  -addext "subjectAltName=DNS:vault,DNS:localhost,IP:127.0.0.1"
sudo chown vault:vault vault-key.pem vault-cert.pem
sudo chmod 600 vault-key.pem
```

---

## 4. ملف الإعداد `vault.hcl`

`/etc/vault.d/vault.hcl`:

```hcl
storage "file" {
  path = "/opt/vault/data"
}

listener "tcp" {
  address       = "0.0.0.0:8200"
  tls_cert_file = "/etc/vault.d/tls/vault-cert.pem"
  tls_key_file  = "/etc/vault.d/tls/vault-key.pem"
}

api_addr     = "https://vault:8200"
cluster_addr = "https://vault:8201"
ui           = true

disable_mlock = true
```

> `disable_mlock = true` لازم لو الـ VPS مالوش `CAP_IPC_LOCK` مفعّلة (شائع على VPS اقتصادية) — بدونها Vault مابيبدأش. لو الـ VPS بتدعمها، امسح السطر ده (أأمن، بيمنع تسريب المفاتيح للـ Swap).

`/etc/systemd/system/vault.service`:

```ini
[Unit]
Description=HashiCorp Vault
Requires=network-online.target
After=network-online.target

[Service]
User=vault
Group=vault
ExecStart=/usr/local/bin/vault server -config=/etc/vault.d/vault.hcl
ExecReload=/bin/kill --signal HUP $MAINPID
Restart=on-failure
RestartSec=5
LimitNOFILE=65536

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now vault
sudo systemctl status vault
```

---

## 5. Init + Unseal (يُنفَّذ مرة واحدة فقط، من طرف مسؤول له صلاحية)

```bash
export VAULT_ADDR="https://127.0.0.1:8200"
export VAULT_SKIP_VERIFY="true"   # شهادة ذاتية التوقيع — يشيل لو الشهادة حقيقية موقّعة من CA معروف

vault operator init -key-shares=5 -key-threshold=3
```

الناتج: **5 Unseal Keys** + **Root Token**. **دي المرة الوحيدة اللي هيظهروا فيها — لازم يتسجلوا فورًا** (قسم 8، النسخ الاحتياطي، تحت) قبل أي خطوة تانية.

```bash
vault operator unseal   # يتنفذ 3 مرات، كل مرة بمفتاح مختلف من الـ 5
vault operator unseal
vault operator unseal

vault login   # بالـ Root Token
```

> **لو السيرفر بيعيد التشغيل** (Reboot، أو Crash)، Vault بيرجع "Sealed" تلقائيًا — لازم `vault operator unseal` ×3 يدويًا تاني. ده سلوك متعمَّد من Vault نفسه (أمان)، مش خطأ.

---

## 6. تفعيل KV v2 وكتابة المفاتيح الأولية

```bash
vault secrets enable -path=secret kv-v2

# مفتاح HMAC — يتولّد عشوائي، مش يُختار يدويًا
vault kv put secret/habbak/pii/hmac-key \
  current="$(openssl rand -hex 32)" \
  previous=""

# مسار مفاتيح Data Protection يتملى تلقائيًا من التطبيق نفسه (VaultDataProtectionRepository) —
# مفيش داعي لكتابة أي حاجة فيه يدويًا هنا.
```

---

## 7. Policy وToken محدود للتطبيق (بديل Root Token في الإنتاج)

**Root Token مايُستخدمش في `appsettings`/Environment Variables بتاع التطبيق أبدًا بعد هذه الخطوة** — يتحفظ بس في مكان النسخ الاحتياطي (قسم 8) للطوارئ (Recovery، Rekey).

`habbak-app-policy.hcl`:

```hcl
path "secret/data/habbak/pii/hmac-key" {
  capabilities = ["read"]
}

path "secret/data/habbak/dataprotection-keys/*" {
  capabilities = ["create", "read", "update", "list"]
}

path "secret/metadata/habbak/dataprotection-keys" {
  capabilities = ["list"]
}
```

```bash
vault policy write habbak-app habbak-app-policy.hcl
vault token create -policy="habbak-app" -period=768h -display-name="habbak-erp-api"
```

الـ Token الناتج ده (**مش Root Token**) هو اللي يتحط في `Vault:Token` بتاع الإنتاج — عن طريق Environment Variable على السيرفر، مش في `appsettings.json` أبدًا:

```bash
# على سيرفر التطبيق، مثلًا في ملف بيئة systemd الخاص بـ Habbak.ERP.API:
Vault__Address=https://vault:8200
Vault__Token=<الـ Token من فوق>
Vault__SkipTlsVerify=false   # true بس لو الشهادة لسه ذاتية التوقيع
```

> `-period=768h` (32 يوم) يخلي الـ Token يتجدد نفسه تلقائيًا طول ما التطبيق بيستخدمه بانتظام (`vault token renew`) — لو التطبيق وقف أكتر من المدة دي، الـ Token بينتهي ولازم Token جديد.

---

## 8. النسخ الاحتياطي (إلزامي، قسم 24 من `00-Project-Overview.md` + قاعدة 8 من `10-Module-HR-Payroll.md`)

| العنصر | إيه اللي يتنسخ | فين | التكرار |
|---|---|---|---|
| **الـ 5 Unseal Keys + Root Token** | نص صريح (أو صورة/PDF مصوّرة) | **مكان منفصل تمامًا عن الـ VPS** — Password Manager مؤسسي (1Password/Bitwarden Business)، أو خزنة فعلية، أو موزّعة على أكتر من شخص مسؤول (كل واحد يمسك مفتاح أو اتنين) | **مرة واحدة عند الإنشاء**، وبعد أي Rekey (قسم 10) |
| `/opt/vault/data` (Storage Backend كامل) | نسخة كاملة للمجلد | نفس نظام النسخ الاحتياطي لقاعدة البيانات (خارج الـ VPS جغرافيًا) | **يومي**، مع سجل المعاملات (لو حصل تعديل كتير) |
| شهادة TLS (`vault-cert.pem`/`vault-key.pem`) | نسخة | مع باقي أسرار السيرفر | عند التوليد/التجديد |

**⚠️ تحذير حرج**: لو الـ VPS ضاع (Hardware Failure) ومفيش نسخة من `/opt/vault/data` **ومفيش Unseal Keys محفوظة برّه السيرفر**، كل البيانات المشفّرة (الأرقام القومية، والحسابات البنكية، ومفاتيح 2FA) **بتضيع نهائيًا ومفيش استرجاع** — نفس التحذير الحرفي في `10-Module-HR-Payroll.md` قاعدة 8.

**اختبار الاستعادة (شهريًا، نفس مبدأ باقي النظام)**:
1. استعد `/opt/vault/data` على سيرفر تجريبي منفصل.
2. شغّل Vault، اعمل Unseal بـ 3 من الـ 5 مفاتيح المحفوظة.
3. تأكد من قراءة `secret/habbak/pii/hmac-key` بنجاح.
4. أي فشل في الخطوات دي = النسخة الاحتياطية مش موثوقة، ولازم تتصلح فورًا.

---

## 9. إجراء الاستعادة (Recovery)

**سيناريو 1 — السيرفر أعيد تشغيله (Reboot عادي، البيانات سليمة)**:
```bash
sudo systemctl start vault   # لو مش شغال أصلًا
vault operator unseal   # ×3
```

**سيناريو 2 — فقدان السيرفر بالكامل، ونسخة احتياطية من `/opt/vault/data` موجودة**:
1. جهّز سيرفر جديد (خطوات 2-4 فوق، عدا `vault operator init` — القاعدة موجودة فعلًا في النسخة).
2. استعد `/opt/vault/data` من النسخة الاحتياطية بمكانها.
3. شغّل الخدمة، اعمل Unseal بـ 3 من الـ 5 مفاتيح المحفوظة (**مش `init` تاني** — ده هينشئ Vault جديد فاضي).
4. تأكد إن التطبيق شغال (`GET /health/vault` يرجع `Healthy`).

**سيناريو 3 — فقدان مفتاح Unseal واحد أو اتنين (أقل من الحد الأدنى 3)**: مفيش مشكلة — 3 من الـ 5 كفاية. لو ضاع 3 أو أكتر: **البيانات مالهاش استرجاع** (سيناريو الكارثة، قسم 8).

---

## 10. تدوير المفاتيح (Key Rotation)

### تدوير مفتاح HMAC (`secret/habbak/pii/hmac-key`)

```bash
CURRENT=$(vault kv get -field=current secret/habbak/pii/hmac-key)
NEW_KEY=$(openssl rand -hex 32)

vault kv put secret/habbak/pii/hmac-key current="$NEW_KEY" previous="$CURRENT"
```

- `HmacPiiHasher` بيعيد قراءة المفتاح تلقائيًا خلال ساعة (Cache Expiry) — مفيش إعادة تشغيل للتطبيق لازمة.
- **مهلة السماح**: أي بحث/تحقق تفرّد بيستخدم `ComputeHashCandidatesAsync` (بيجرب `current` و`previous` الاتنين) لحد ما كل البيانات المكتوبة بالمفتاح القديم تتراجع/تتحدث. بعد فترة كافية (شهر مقترح)، شغّل التدوير تاني — وقتها `previous` القديم بيضيع نهائيًا (لو فيه بيانات لسه بتعتمد عليه، لازم تتحدث الأول).

### تدوير Token التطبيق (`habbak-app`)

```bash
vault token renew <الـ Token الحالي>   # يمدد المدة من غير Token جديد
# أو لو محتاج Token جديد بالكامل (تسريب مشتبه فيه):
vault token revoke <الـ Token القديم>
vault token create -policy="habbak-app" -period=768h -display-name="habbak-erp-api"
# وتحديث Vault__Token على سيرفر التطبيق + إعادة تشغيل الخدمة
```

### Rekey (تغيير عدد/نسبة الـ Unseal Keys نفسها — نادر)

```bash
vault operator rekey -init -key-shares=5 -key-threshold=3
# كل واحد من حاملي المفاتيح القدامى يدخل مفتاحه بالترتيب لحد ما العملية تخلص
# الناتج: 5 مفاتيح جديدة تمامًا — القديمة تتلغى، والجديدة تتحفظ فورًا (قسم 8)
```

---

## وضع التطوير المحلي (Dev Mode — للمقارنة بس، مش للإنتاج)

```bash
vault server -dev -dev-root-token-id="dev-root-token" -dev-listen-address="127.0.0.1:8200"
```

- تخزين في الذاكرة (كل حاجة بتضيع عند إيقاف العملية) — بيتفعّل ويتحل تلقائيًا (Auto-Unseal)، من غير HTTPS، وToken ثابت.
- بعد التشغيل، اتفعل الـ KV engine وكُتب مفتاح الـ HMAC مرة واحدة يدويًا (نفس أمر قسم 6 فوق، بس من غير `sudo` وعلى الجهاز المحلي).
- `appsettings.Development.json` بيتوقع بالظبط الإعداد ده (`Address: http://localhost:8200`, `Token: dev-root-token`) — أي مطوّر لازم يشغّل الأمر ده يدويًا قبل ما يشغّل الـ API محليًا (ده أثر جانبي مهم لقرار "Vault كامل من غير Fallback دائم" — موثّق كـ Issue في تقرير التسليم).

---

## 🚧 مؤجَّل: إعداد Vault لـ Data Protection + JWT Signing (بيئة Sandbox/CI جديدة تمامًا)

> **الحالة: مؤجَّل، مش عاجل** (`Phase-3B-Cloud-Report.md §7`). القسم فوق (مفتاح الـHMAC بس) كافي
> لجهاز تطوير محلي مُعَد بالفعل من زمان — لكنه مش كافي لبيئة Sandbox/CI جديدة تمامًا اتفعّل فيها
> Vault لأول مرة: 19 اختبار في `Habbak.ERP.ApiTests` (2FA، Login/Session، PII Reveal، JWT
> الحقيقي) فشلوا في بيئة Cloud Sandbox بالظبط لهذا السبب، رغم إن Vault نفسه كان شغّال وسليم
> (`VaultIntegrationTests`/`HmacPiiHasherTests` نجحوا 7/7) — يبقى الفجوة في إعداد الـData
> Protection Keys و/أو JWT Signing Key نفسهم، مش في الاتصال بـVault.
>
> **لسه مش موثّق هنا**: الخطوات الدقيقة اللي جهاز التطوير المحلي "بيعرفها ضمنيًا" (مُعَد من زمان)
> بس بيئة جديدة محتاجاها صراحة — مثلًا تفعيل مسار `secret/habbak/dataprotection-keys/*` بشكل
> يسمح بالكتابة الأولى منه (`VaultDataProtectionRepository`)، وأي إعداد إضافي لمفتاح توقيع الـJWT
> لو هو كمان بيتخزن/يتشتق من Vault. يحتاج تحقيق مخصص (مش جزء من Phase 3B) قبل ما يُكتب هنا كخطوات
> نهائية.
