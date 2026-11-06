# ABC.Billing.App.Client (esqueleto)

Aplicación cliente de punto de venta (.NET MAUI: Windows y Android). **Hoy solo existe la vista `PosBillingPage`**
como esqueleto; no forma parte de la solución (`ABC.Billing.sln`) porque requiere las cargas de trabajo de MAUI.

Responsabilidad prevista: pantallas de caja que consumen la API de `docs/openapi.yaml`. No contiene reglas de
negocio ni calcula impuestos. Empaquetado: MSI (`packaging/windows-msi`) y APK (`packaging/android-apk`).
