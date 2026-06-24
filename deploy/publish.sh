#!/usr/bin/env bash
# ============================================================
#  نشر البرنامج كملف ويندوز تنفيذي واحد — من ماك أو لينكس.
#  .NET يبني win-x64 عبر المنصات، فلا حاجة لجهاز ويندوز هنا.
#  المتطلب: .NET 8 SDK مثبّت على الماك  (dotnet --version => 8.x)
#  التشغيل:  ./deploy/publish.sh   (من جذر المشروع)
# ============================================================
set -e

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/deploy/publish"

echo "=== نشر Hikvision Attendance (win-x64, self-contained) ==="
dotnet publish "$ROOT/src/Hikvision.Web/Hikvision.Web.csproj" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o "$OUT"

echo ""
echo "تم النشر بنجاح."
echo "الملف التنفيذي:  $OUT/Hikvision.Web.exe"
echo "انسخ كامل محتويات المجلد deploy/publish إلى جهاز ويندوز (مثلًا C:\\HikvisionApp)،"
echo "ثم شغّل عليه deploy\\install-service.bat كمسؤول."
