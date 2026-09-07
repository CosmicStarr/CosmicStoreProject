"""Generate CosmicStore project outline Word document."""
from docx import Document
from docx.shared import Pt, Inches, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH

doc = Document()

# Title
title = doc.add_heading("CosmicStore Project Outline", 0)
title.alignment = WD_ALIGN_PARAGRAPH.CENTER

subtitle = doc.add_paragraph("CJ Dropshipping E-Commerce Store")
subtitle.alignment = WD_ALIGN_PARAGRAPH.CENTER
subtitle.runs[0].font.size = Pt(14)
subtitle.runs[0].font.color.rgb = RGBColor(0x64, 0x74, 0x8B)

meta = doc.add_paragraph("Stack: Angular · .NET Web API · Redis · MS SQL Server · Entity Framework\nDual DbContext: CJ Dropshipping (staging) + Store/Identity (storefront)")
meta.alignment = WD_ALIGN_PARAGRAPH.CENTER
meta.runs[0].font.size = Pt(10)
meta.runs[0].font.italic = True

doc.add_paragraph()

# Executive summary
doc.add_heading("Executive Summary", level=1)
doc.add_paragraph(
    "CosmicStore is a CJ dropshipping e-commerce platform with a solid foundation: dual-database architecture, "
    "CJ product sync, cached storefront API, Identity/JWT authentication, and partial admin tooling. "
    "Phase 0 foundation fixes are complete. The project is not yet a fully shoppable store — cart, checkout, "
    "payments, order tracking, and several auth/admin protections remain."
)

# Current state
doc.add_heading("What You Already Have", level=1)
table = doc.add_table(rows=1, cols=2)
table.style = "Table Grid"
hdr = table.rows[0].cells
hdr[0].text = "Layer"
hdr[1].text = "Completed Features"
for cell in hdr:
    for p in cell.paragraphs:
        for r in p.runs:
            r.bold = True

rows = [
    ("CJ Integration", "Auth manager, 6-hour sync worker → FlatProducts/FlatCategories, order create/pay service"),
    ("Catalog Pipeline", "Admin edit API → store.Products/ProductImages; storefront via stored procs + Redis cache"),
    ("Authentication", "Register/login API, JWT interceptor, token persistence, login UI"),
    ("Frontend", "Home, store listing, product detail (read-only), register, partial admin dashboard/edit"),
    ("Infrastructure", "Exception middleware, pagination headers, CORS, Redis caching, user secrets"),
    ("Phase 0 Fixes", "DI registration, JWT/Redis config alignment, CJ config binding, routing fixes, edit form population"),
]
for layer, features in rows:
    row = table.add_row().cells
    row[0].text = layer
    row[1].text = features

doc.add_paragraph()

# Architecture
doc.add_heading("Architecture Overview", level=1)
doc.add_paragraph("Two-tier product data flow:")
flow = doc.add_paragraph(style="List Bullet")
flow.add_run("CJ API → CJProductSyncWorker → FlatProducts/FlatCategories (ApplicationDbContext)")
flow = doc.add_paragraph(style="List Bullet")
flow.add_run("Admin edits → EditProductsController → Products/ProductImages (store schema)")
flow = doc.add_paragraph(style="List Bullet")
flow.add_run("Storefront API → stored procedures → Angular UI")

doc.add_paragraph()
doc.add_heading("DbContext Split", level=2)
ctx_table = doc.add_table(rows=1, cols=3)
ctx_table.style = "Table Grid"
h = ctx_table.rows[0].cells
h[0].text = "DbContext"
h[1].text = "Purpose"
h[2].text = "Key Entities"
for c in h:
    for p in c.paragraphs:
        for r in p.runs:
            r.bold = True
ctx_rows = [
    ("ApplicationDbContext", "CJ staging / admin catalog", "FlatProduct, FlatCategory"),
    ("ApplicationDbStoreContext", "Storefront + Identity", "Products, ProductImage, AppUser (Identity)"),
]
for a, b, c in ctx_rows:
    r = ctx_table.add_row().cells
    r[0].text = a
    r[1].text = b
    r[2].text = c

doc.add_paragraph()

# Phases
phases = [
    ("Phase 0 — Foundation Fixes", "COMPLETE", [
        "Register ICJDropshippingService, IShoppingCartService, IDatabase, IOptions<CjAuthRequest>",
        "Fix JWT ValidIssuer/ValidAudience typo alignment",
        "Unify Redis connection string to RedisConnection",
        "Build login UI, fix navigation routes, product links, edit form population",
        "Move JWT secret and CJ API key to user secrets",
    ]),
    ("Phase 1 — Core Shopping Flow (Must-Have)", "NEXT", [
        "Shopping Cart: CartController, cart service wiring, guest/user cart merge, Angular cart UI",
        "Checkout & Payment: Order/OrderItem persistence, Stripe/PayPal, SKU→CJ vid mapping",
        "Order Management: status tracking, user order history, CJ webhook/polling for shipments",
    ]),
    ("Phase 2 — Catalog & Admin Pipeline", "PENDING", [
        "FlatProducts → store publish flow (manual, bulk, or auto with markup rules)",
        "Fix EditCjProducts to update existing products instead of always inserting",
        "Create SQL scripts for stored procedures (GetAllProductsWithPictures, etc.)",
        "Complete admin layout, auth guards, orders/users/settings pages",
    ]),
    ("Phase 3 — Auth & User Features", "PENDING", [
        "Email confirmation and password reset pages + EmailSender implementation",
        "Auth guards (authGuard, adminGuard) on Angular routes",
        "Profile, wishlist, saved addresses for checkout",
    ]),
    ("Phase 4 — Storefront Polish", "PENDING", [
        "Wire search, filter, sort, pagination to API",
        "Real stock count from CJ, related products",
        "Fix remaining broken footer/nav links",
    ]),
    ("Phase 5 — CJ Dropshipping Operations", "PENDING", [
        "SKU/VID mapping in database",
        "Shipping method selection (currently hardcoded)",
        "CJ wallet balance monitoring",
        "Order status webhooks, returns/refunds",
        "Incremental sync, price markup, stock sync, image hosting",
    ]),
    ("Phase 6 — Infrastructure & Production", "PENDING", [
        "Docker Compose, CI/CD pipeline",
        "Production CORS, HTTPS, health checks",
        "Structured logging (Serilog), error tracking (Sentry/App Insights)",
        "Integration, component, and E2E tests",
    ]),
]

for phase_name, status, items in phases:
    doc.add_heading(phase_name, level=1)
    p = doc.add_paragraph()
    run = p.add_run(f"Status: {status}")
    run.bold = True
    if status == "COMPLETE":
        run.font.color.rgb = RGBColor(0x16, 0xA3, 0x4A)
    elif status == "NEXT":
        run.font.color.rgb = RGBColor(0xFF, 0x91, 0x00)
    else:
        run.font.color.rgb = RGBColor(0x64, 0x74, 0x8B)
    for item in items:
        doc.add_paragraph(item, style="List Bullet")

doc.add_paragraph()

# Priority table
doc.add_heading("Suggested Priority Order", level=1)
pri = doc.add_table(rows=1, cols=3)
pri.style = "Table Grid"
ph = pri.rows[0].cells
ph[0].text = "Priority"
ph[1].text = "Work"
ph[2].text = "Why"
for c in ph:
    for p in c.paragraphs:
        for r in p.runs:
            r.bold = True
priorities = [
    ("P0", "Fix DI, JWT, Redis config, login UI", "Unblocks all other work — DONE"),
    ("P1", "Cart (API + UI)", "Can't shop without it"),
    ("P2", "Checkout + Stripe + order persistence", "Revenue path"),
    ("P3", "Admin guards + publish pipeline", "Catalog management"),
    ("P4", "Email + auth pages", "User trust"),
    ("P5", "CJ webhooks + VID mapping", "Reliable fulfillment"),
    ("P6", "Deploy + monitoring", "Go live"),
]
for a, b, c in priorities:
    r = pri.add_row().cells
    r[0].text = a
    r[1].text = b
    r[2].text = c

doc.add_paragraph()

# Exists vs Missing
doc.add_heading("Quick Reference: Exists vs Missing", level=1)
ref = doc.add_table(rows=1, cols=2)
ref.style = "Table Grid"
rh = ref.rows[0].cells
rh[0].text = "Exists (code written)"
rh[1].text = "Still Missing"
for c in rh:
    for p in c.paragraphs:
        for r in p.runs:
            r.bold = True
refs = [
    ("ShoppingCartService.cs", "Cart controller, frontend service, UI"),
    ("CJDropshippingService.cs", "Was unregistered — now registered; checkout UI needed"),
    ("Order.cs, OrderItem.cs", "DbContext DbSets, migration, controller"),
    ("EmailSender.cs", "Actual SMTP/SendGrid implementation"),
    ("ConfirmEmail.html template", "Angular route + confirm endpoint"),
    ("AngularCheckoutRequest.cs", "Checkout UI + Stripe integration"),
    ("Admin dashboard/edit", "Guards, layout shell, sub-pages"),
]
for a, b in refs:
    r = ref.add_row().cells
    r[0].text = a
    r[1].text = b

doc.add_paragraph()

# API endpoints
doc.add_heading("Current API Endpoints", level=1)
api = doc.add_table(rows=1, cols=3)
api.style = "Table Grid"
ah = api.rows[0].cells
ah[0].text = "Controller"
ah[1].text = "Endpoints"
ah[2].text = "Auth"
for c in ah:
    for p in c.paragraphs:
        for r in p.runs:
            r.bold = True
apis = [
    ("AccountController", "POST register, POST login, GET current user", "GET only"),
    ("ProductsController", "GET joined-products, GET {id}", "None"),
    ("HomeController", "GET highlighted/{type}, GET {id}", "None"),
    ("EditProductsController", "GET all, GET {id}, POST UpdateProduct/{id}", "None — needs Admin"),
    ("OrdersController", "POST checkout", "None"),
]
for a, b, c in apis:
    r = api.add_row().cells
    r[0].text = a
    r[1].text = b
    r[2].text = c

doc.add_paragraph()

# Angular routes
doc.add_heading("Current Angular Routes", level=1)
routes = doc.add_table(rows=1, cols=3)
routes.style = "Table Grid"
rt = routes.rows[0].cells
rt[0].text = "Route"
rt[1].text = "Component"
rt[2].text = "Status"
for c in rt:
    for p in c.paragraphs:
        for r in p.runs:
            r.bold = True
route_rows = [
    ("/", "HomeComponent", "Implemented"),
    ("/store", "ProductsComponent", "Implemented"),
    ("/store/:id", "ProductDetailComponent", "Read-only"),
    ("/login", "LoginComponent", "Implemented (Phase 0)"),
    ("/register", "RegisterComponent", "Implemented"),
    ("/admin/dashboard", "DashboardComponent", "Partial"),
    ("/admin/edit-Product/:id", "EditProductComponent", "Partial"),
    ("/cart, /checkout, /orders", "—", "Not built"),
]
for a, b, c in route_rows:
    r = routes.add_row().cells
    r[0].text = a
    r[1].text = b
    r[2].text = c

doc.add_paragraph()
footer = doc.add_paragraph("Generated for CosmicStoreProject · September 2026")
footer.alignment = WD_ALIGN_PARAGRAPH.CENTER
footer.runs[0].font.size = Pt(9)
footer.runs[0].font.italic = True
footer.runs[0].font.color.rgb = RGBColor(0x94, 0xA3, 0xB8)

output_path = r"C:\Users\Norma\CosmicStoreProject\docs\CosmicStore-Project-Outline.docx"
doc.save(output_path)
print(f"Created: {output_path}")
