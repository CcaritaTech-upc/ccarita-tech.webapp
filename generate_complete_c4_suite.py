import os
import subprocess
import shutil
from PIL import Image

EDGE_PATH = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
DOCS_DIAGRAMS_DIR = r"C:\Users\axelm\OneDrive\Documentos\MisArchivos\Upc-202602\Diseño\docs\diagrams"
ASSETS_DIR = r"C:\Users\axelm\OneDrive\Documentos\MisArchivos\Upc-202602\Emergentes\assets"

os.makedirs(DOCS_DIAGRAMS_DIR, exist_ok=True)
os.makedirs(ASSETS_DIR, exist_ok=True)

# -------------------------------------------------------------
# SHAPE BUILDERS (Structurizr Showcase 12541 Equivalent)
# -------------------------------------------------------------

def person_box(x, y, w, h, name, desc):
    """Shape Person: Silhouette with head circle and rounded body"""
    return f"""
    <g transform="translate({x},{y})">
        <circle cx="{w/2}" cy="-16" r="22" fill="#08427B" stroke="#052e56" stroke-width="2.5" filter="url(#shadow)"/>
        <rect x="0" y="0" width="{w}" height="{h}" rx="14" fill="#08427B" stroke="#052e56" stroke-width="2.5" filter="url(#shadow)"/>
        <text x="{w/2}" y="28" font-family="'Segoe UI', Roboto, sans-serif" font-size="18" font-weight="700" fill="#ffffff" text-anchor="middle">{name}</text>
        <text x="{w/2}" y="48" font-family="'Segoe UI', Roboto, sans-serif" font-size="12" fill="#b0cde8" text-anchor="middle">[Person]</text>
        <foreignObject x="10" y="58" width="{w-20}" height="{h-65}">
            <div xmlns="http://www.w3.org/1999/xhtml" style="font-family:'Segoe UI', Roboto, sans-serif; font-size:12px; color:#e0effc; text-align:center; line-height:1.35;">
                {desc}
            </div>
        </foreignObject>
    </g>
    """

def system_box(x, y, w, h, name, desc, is_external=False):
    """Shape RoundedBox: Software System"""
    bg = "#8A9BA8" if is_external else "#1168BD"
    border = "#5f717d" if is_external else "#0c4f91"
    sub_color = "#e6edf2" if is_external else "#b7d6f7"
    type_label = "[Software System - Externo]" if is_external else "[Software System Central]"
    return f"""
    <g transform="translate({x},{y})">
        <rect x="0" y="0" width="{w}" height="{h}" rx="14" fill="{bg}" stroke="{border}" stroke-width="2.5" filter="url(#shadow)"/>
        <text x="{w/2}" y="32" font-family="'Segoe UI', Roboto, sans-serif" font-size="19" font-weight="700" fill="#ffffff" text-anchor="middle">{name}</text>
        <text x="{w/2}" y="52" font-family="'Segoe UI', Roboto, sans-serif" font-size="12" fill="{sub_color}" text-anchor="middle">{type_label}</text>
        <foreignObject x="12" y="62" width="{w-24}" height="{h-70}">
            <div xmlns="http://www.w3.org/1999/xhtml" style="font-family:'Segoe UI', Roboto, sans-serif; font-size:12.5px; color:#ffffff; text-align:center; line-height:1.4;">
                {desc}
            </div>
        </foreignObject>
    </g>
    """

def web_browser_box(x, y, w, h, name, tech, desc):
    """Shape WebBrowser: Window frame with 3 dots and URL address bar"""
    return f"""
    <g transform="translate({x},{y})">
        <!-- Main window -->
        <rect x="0" y="0" width="{w}" height="{h}" rx="10" fill="#2A72C9" stroke="#1c5599" stroke-width="2" filter="url(#shadow)"/>
        <!-- Window title bar -->
        <rect x="0" y="0" width="{w}" height="24" rx="10" fill="#1c5599"/>
        <rect x="0" y="14" width="{w}" height="10" fill="#1c5599"/>
        <!-- 3 window control dots -->
        <circle cx="12" cy="12" r="4" fill="#ff5f56"/>
        <circle cx="24" cy="12" r="4" fill="#ffbd2e"/>
        <circle cx="36" cy="12" r="4" fill="#27c93f"/>
        <!-- Address bar placeholder -->
        <rect x="52" y="6" width="{w-64}" height="12" rx="3" fill="#ffffff" opacity="0.3"/>
        <!-- Texts -->
        <text x="{w/2}" y="48" font-family="'Segoe UI', Roboto, sans-serif" font-size="17" font-weight="700" fill="#ffffff" text-anchor="middle">{name}</text>
        <text x="{w/2}" y="67" font-family="'Segoe UI', Roboto, sans-serif" font-size="11" fill="#cbe2fc" text-anchor="middle">[Container: WebBrowser - {tech}]</text>
        <foreignObject x="10" y="75" width="{w-20}" height="{h-80}">
            <div xmlns="http://www.w3.org/1999/xhtml" style="font-family:'Segoe UI', Roboto, sans-serif; font-size:12px; color:#ffffff; text-align:center; line-height:1.35;">
                {desc}
            </div>
        </foreignObject>
    </g>
    """

def mobile_device_box(x, y, w, h, name, tech, desc):
    """Shape MobileDevicePortrait: Smartphone frame with speaker notch"""
    return f"""
    <g transform="translate({x},{y})">
        <rect x="0" y="0" width="{w}" height="{h}" rx="18" fill="#2A72C9" stroke="#1c5599" stroke-width="2.5" filter="url(#shadow)"/>
        <!-- Speaker / sensor top -->
        <rect x="{w/2 - 20}" y="6" width="40" height="4" rx="2" fill="#1c5599"/>
        <circle cx="{w/2 + 28}" cy="8" r="2.5" fill="#1c5599"/>
        <!-- Screen area border -->
        <rect x="6" y="18" width="{w-12}" height="{h-26}" rx="10" fill="none" stroke="#6ba7ee" stroke-width="1"/>
        <text x="{w/2}" y="44" font-family="'Segoe UI', Roboto, sans-serif" font-size="17" font-weight="700" fill="#ffffff" text-anchor="middle">{name}</text>
        <text x="{w/2}" y="63" font-family="'Segoe UI', Roboto, sans-serif" font-size="11" fill="#cbe2fc" text-anchor="middle">[Container: MobileDevice - {tech}]</text>
        <foreignObject x="12" y="72" width="{w-24}" height="{h-80}">
            <div xmlns="http://www.w3.org/1999/xhtml" style="font-family:'Segoe UI', Roboto, sans-serif; font-size:12px; color:#ffffff; text-align:center; line-height:1.35;">
                {desc}
            </div>
        </foreignObject>
    </g>
    """

def container_box(x, y, w, h, name, tech, desc):
    """Shape RoundedBox: General Container (API / Service / Proxy)"""
    return f"""
    <g transform="translate({x},{y})">
        <rect x="0" y="0" width="{w}" height="{h}" rx="12" fill="#2A72C9" stroke="#1c5599" stroke-width="2" filter="url(#shadow)"/>
        <text x="{w/2}" y="28" font-family="'Segoe UI', Roboto, sans-serif" font-size="17" font-weight="700" fill="#ffffff" text-anchor="middle">{name}</text>
        <text x="{w/2}" y="47" font-family="'Segoe UI', Roboto, sans-serif" font-size="11" fill="#cbe2fc" text-anchor="middle">[Container: {tech}]</text>
        <foreignObject x="10" y="55" width="{w-20}" height="{h-60}">
            <div xmlns="http://www.w3.org/1999/xhtml" style="font-family:'Segoe UI', Roboto, sans-serif; font-size:12px; color:#ffffff; text-align:center; line-height:1.35;">
                {desc}
            </div>
        </foreignObject>
    </g>
    """

def database_box(x, y, w, h, name, tech, desc, is_external=False):
    """Shape Cylinder: Database 3D Cylinder"""
    bg = "#7f8c8d" if is_external else "#1F618D"
    top_bg = "#95a5a6" if is_external else "#2980B9"
    border = "#515a5a" if is_external else "#154360"
    type_label = f"[Database Externo: {tech}]" if is_external else f"[Container: Database ({tech})]"
    return f"""
    <g transform="translate({x},{y})">
        <path d="M 0,20 L 0,{h-20} A {w/2},20 0 0 0 {w},{h-20} L {w},20 Z" fill="{bg}" stroke="{border}" stroke-width="2.5" filter="url(#shadow)"/>
        <ellipse cx="{w/2}" cy="{h-20}" rx="{w/2}" ry="20" fill="{bg}" stroke="{border}" stroke-width="2.5"/>
        <ellipse cx="{w/2}" cy="20" rx="{w/2}" ry="20" fill="{top_bg}" stroke="{border}" stroke-width="2.5"/>
        <text x="{w/2}" y="58" font-family="'Segoe UI', Roboto, sans-serif" font-size="17" font-weight="700" fill="#ffffff" text-anchor="middle">{name}</text>
        <text x="{w/2}" y="78" font-family="'Segoe UI', Roboto, sans-serif" font-size="11" fill="#d4e6f1" text-anchor="middle">{type_label}</text>
        <foreignObject x="12" y="88" width="{w-24}" height="{h-95}">
            <div xmlns="http://www.w3.org/1999/xhtml" style="font-family:'Segoe UI', Roboto, sans-serif; font-size:12px; color:#ffffff; text-align:center; line-height:1.35;">
                {desc}
            </div>
        </foreignObject>
    </g>
    """

def pipe_box(x, y, w, h, name, tech, desc):
    """Shape Pipe: Horizontal Cylinder / Queue for MQTT Broker"""
    return f"""
    <g transform="translate({x},{y})">
        <!-- Horizontal pipe body -->
        <rect x="20" y="0" width="{w-40}" height="{h}" fill="#1F618D" stroke="#154360" stroke-width="2" filter="url(#shadow)"/>
        <!-- Left end cap -->
        <ellipse cx="20" cy="{h/2}" rx="18" ry="{h/2}" fill="#2980B9" stroke="#154360" stroke-width="2"/>
        <!-- Right end cap -->
        <ellipse cx="{w-20}" cy="{h/2}" rx="18" ry="{h/2}" fill="#1F618D" stroke="#154360" stroke-width="2"/>
        <text x="{w/2}" y="32" font-family="'Segoe UI', Roboto, sans-serif" font-size="17" font-weight="700" fill="#ffffff" text-anchor="middle">{name}</text>
        <text x="{w/2}" y="52" font-family="'Segoe UI', Roboto, sans-serif" font-size="11" fill="#d4e6f1" text-anchor="middle">[Container: Pipe / Queue ({tech})]</text>
        <foreignObject x="25" y="60" width="{w-50}" height="{h-65}">
            <div xmlns="http://www.w3.org/1999/xhtml" style="font-family:'Segoe UI', Roboto, sans-serif; font-size:12px; color:#ffffff; text-align:center; line-height:1.35;">
                {desc}
            </div>
        </foreignObject>
    </g>
    """

def component_box(x, y, w, h, name, tech, desc):
    """Shape Component: Box with 2 UML component tabs on top-left"""
    return f"""
    <g transform="translate({x},{y})">
        <rect x="0" y="0" width="{w}" height="{h}" rx="10" fill="#438DD5" stroke="#2b6fb5" stroke-width="2" filter="url(#shadow)"/>
        <!-- 2 UML component tabs -->
        <rect x="-8" y="16" width="16" height="14" rx="2" fill="#2b6fb5"/>
        <rect x="-8" y="38" width="16" height="14" rx="2" fill="#2b6fb5"/>
        <text x="{w/2}" y="26" font-family="'Segoe UI', Roboto, sans-serif" font-size="16" font-weight="700" fill="#ffffff" text-anchor="middle">{name}</text>
        <text x="{w/2}" y="44" font-family="'Segoe UI', Roboto, sans-serif" font-size="11" fill="#d9ecff" text-anchor="middle">[Component: {tech}]</text>
        <foreignObject x="10" y="52" width="{w-20}" height="{h-56}">
            <div xmlns="http://www.w3.org/1999/xhtml" style="font-family:'Segoe UI', Roboto, sans-serif; font-size:11px; color:#ffffff; text-align:center; line-height:1.3;">
                {desc}
            </div>
        </foreignObject>
    </g>
    """

def deployment_node_box(x, y, w, h, name, tech, desc=""):
    """Deployment Node: 3D-like box for infrastructure / host / environment"""
    desc_markup = f'<text x="24" y="54" font-family="Segoe UI, sans-serif" font-size="12" fill="#64748b">{desc}</text>' if desc else ""
    return f"""
    <g transform="translate({x},{y})">
        <!-- Isometric top tab / 3D feel -->
        <path d="M 0,16 L 16,0 L {w},0 L {w-16},16 Z" fill="#e2e8f0" stroke="#94a3b8" stroke-width="1.5"/>
        <rect x="0" y="16" width="{w}" height="{h-16}" rx="8" fill="#ffffff" stroke="#94a3b8" stroke-width="2" stroke-dasharray="6,4" opacity="0.97"/>
        <text x="24" y="38" font-family="'Segoe UI', sans-serif" font-size="16" font-weight="700" fill="#1e293b">{name} <tspan font-weight="400" font-size="12" fill="#64748b">[{tech}]</tspan></text>
        {desc_markup}
    </g>
    """

def draw_arrow(x1, y1, x2, y2, label="", protocol="", dashed=False, label_dx=0, label_dy=0, label_w=170):
    dash_attr = 'stroke-dasharray="6,5"' if dashed else ''
    mx = (x1 + x2) / 2 + label_dx
    my = (y1 + y2) / 2 + label_dy
    
    label_markup = ""
    if label or protocol:
        lines = []
        if label: lines.append(f'<div style="font-weight:600; color:#222; font-size:11px;">{label}</div>')
        if protocol: lines.append(f'<div style="font-weight:400; color:#555; font-size:10px; margin-top:2px;">[{protocol}]</div>')
        content = "".join(lines)
        label_markup = f"""
        <g transform="translate({mx - label_w/2}, {my - 20})">
            <rect width="{label_w}" height="38" rx="6" fill="#ffffff" stroke="#bbbbbb" stroke-width="1" filter="url(#mini-shadow)" opacity="0.96"/>
            <foreignObject x="0" y="2" width="{label_w}" height="34">
                <div xmlns="http://www.w3.org/1999/xhtml" style="font-family:'Segoe UI', sans-serif; text-align:center;">
                    {content}
                </div>
            </foreignObject>
        </g>
        """

    return f"""
    <path d="M {x1},{y1} L {x2},{y2}" stroke="#666666" stroke-width="2" {dash_attr} marker-end="url(#arrowhead)"/>
    {label_markup}
    """

def draw_curved_arrow(x1, y1, cx, cy, x2, y2, label="", protocol="", label_w=170):
    mx = (x1 + 2*cx + x2) / 4
    my = (y1 + 2*cy + y2) / 4
    label_markup = ""
    if label or protocol:
        lines = []
        if label: lines.append(f'<div style="font-weight:600; color:#222; font-size:11px;">{label}</div>')
        if protocol: lines.append(f'<div style="font-weight:400; color:#555; font-size:10px; margin-top:2px;">[{protocol}]</div>')
        content = "".join(lines)
        label_markup = f"""
        <g transform="translate({mx - label_w/2}, {my - 20})">
            <rect width="{label_w}" height="36" rx="6" fill="#ffffff" stroke="#bbbbbb" stroke-width="1" filter="url(#mini-shadow)" opacity="0.96"/>
            <foreignObject x="0" y="2" width="{label_w}" height="32">
                <div xmlns="http://www.w3.org/1999/xhtml" style="font-family:'Segoe UI', sans-serif; text-align:center;">
                    {content}
                </div>
            </foreignObject>
        </g>
        """
    return f"""
    <path d="M {x1},{y1} Q {cx},{cy} {x2},{y2}" stroke="#666666" stroke-width="2" fill="none" marker-end="url(#arrowhead)"/>
    {label_markup}
    """

# -------------------------------------------------------------
# 1. DIAGRAMA DE CONTEXTO (NIVEL 1 - 2 SEGMENTOS OBJETIVO)
# -------------------------------------------------------------
def build_context_svg():
    width, height = 1800, 1100
    svg = f"""
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" width="{width}" height="{height}" style="background-color:#f9fbfd;">
    <defs>
        <filter id="shadow" x="-5%" y="-5%" width="112%" height="116%" filterUnits="userSpaceOnUse">
            <feDropShadow dx="2" dy="5" stdDeviation="5" flood-color="#000000" flood-opacity="0.18"/>
        </filter>
        <filter id="mini-shadow" x="-5%" y="-5%" width="110%" height="120%" filterUnits="userSpaceOnUse">
            <feDropShadow dx="1" dy="2" stdDeviation="2" flood-color="#000000" flood-opacity="0.15"/>
        </filter>
        <marker id="arrowhead" markerWidth="10" markerHeight="7" refX="9" refY="3.5" orient="auto">
            <polygon points="0 0, 10 3.5, 0 7" fill="#666666"/>
        </marker>
    </defs>

    <!-- Header -->
    <rect x="40" y="30" width="{width-80}" height="70" rx="8" fill="#ffffff" stroke="#e1e8ed" stroke-width="1.5"/>
    <text x="70" y="66" font-family="'Segoe UI', Roboto, sans-serif" font-size="24" font-weight="700" fill="#1c3d5a">[C4 Nivel 1: System Context] IoBuild Platform - Gestión Inteligente y Prorrateo IoT</text>
    <text x="70" y="88" font-family="'Segoe UI', Roboto, sans-serif" font-size="14" fill="#6b7c96">Delimita las interacciones exclusivas con los 2 segmentos de clientes objetivo y los 6 sistemas externos</text>

    <!-- 2 PERSONAS OBJETIVO (Izquierda) -->
    {person_box(100, 260, 280, 160, "Administrador de Condominios", "Segmento Objetivo B2B: Property Manager que configura catastro, valida alícuotas, aprueba liquidaciones y supervisa cobros.")}
    {person_box(100, 680, 280, 160, "Propietario / Residente", "Segmento Objetivo B2C: Copropietario que visualiza consumos en tiempo real, abona cuotas con tarjeta y consulta al asistente IA.")}

    <!-- SISTEMA CENTRAL (Centro) -->
    {system_box(670, 420, 380, 250, "IoBuild Platform", "Plataforma central SaaS/HaaS para gestión de condominios: ingestión de telemetría IoT de agua/luz, cálculo de alícuotas y liquidación matemática de gastos comunes, pasarela de cobros y copiloto cognitivo RAG.")}

    <!-- SISTEMAS EXTERNOS (Derecha y Arriba) -->
    {system_box(1380, 130, 300, 140, "Stripe Payments Platform", "Pasarela de pagos para planes de suscripción B2B y recaudación de cuotas de mantenimiento con tarjeta.", is_external=True)}
    {database_box(1380, 330, 300, 150, "Cloudinary CDN", "Cloud Storage", "Custodia y optimización de comprobantes bancarios escaneados y fotografías de filtraciones.", is_external=True)}
    {system_box(1380, 540, 300, 140, "Google Gemini / OpenAI API", "Modelos fundacionales LLM para análisis RAG, soporte en lenguaje natural y mantenimiento predictivo.", is_external=True)}
    {system_box(1380, 740, 300, 140, "Servicio de Notificaciones", "Firebase FCM y correo transaccional para alertas críticas de fuga nocturna y recibos digitales.", is_external=True)}
    {pipe_box(1380, 940, 300, 130, "Eclipse Mosquitto", "MQTT Broker", "Broker pub/sub para ingesta y encolamiento asíncrono de telemetría de campo.")}
    {system_box(720, 140, 280, 140, "Medidores y Sensores IoT", "Hardware edge: medidores ultrasónicos de agua y analizadores de red eléctrica en tableros.", is_external=True)}

    <!-- RELACIONES -->
    {draw_arrow(380, 340, 670, 480, "Gestiona catastro y liquidaciones", "HTTPS / Web Portal")}
    {draw_arrow(380, 760, 670, 610, "Visualiza consumos y paga cuotas", "HTTPS / Mobile App y Web")}

    {draw_arrow(860, 280, 1380, 970, "Transmite lecturas cada 60s", "MQTT :1883", label_dx=20, label_dy=10)}
    {draw_arrow(1380, 1000, 980, 670, "Ingesta asíncrona de telemetría", "MQTT / TCP", label_dx=50, label_dy=10)}

    {draw_arrow(1050, 470, 1380, 200, "Procesa cargos de suscripción y recibos", "REST / HTTPS")}
    {draw_arrow(1050, 510, 1380, 400, "Almacena y optimiza vouchers y fotos", "REST / HTTPS")}
    {draw_arrow(1050, 560, 1380, 600, "Inyecta contexto RAG y recibe inferencias", "REST / HTTPS")}
    {draw_arrow(1050, 620, 1380, 800, "Despacha avisos de cobro y alertas", "REST / HTTPS")}

    <!-- Leyenda C4 Oficial -->
    <g transform="translate(60, 990)">
        <rect width="780" height="60" rx="8" fill="#ffffff" stroke="#d0dbe5" stroke-width="1"/>
        <circle cx="32" cy="30" r="10" fill="#08427B"/>
        <text x="52" y="35" font-family="'Segoe UI', sans-serif" font-size="13" font-weight="600" fill="#222222">Persona (shape Person)</text>
        <rect x="230" y="18" width="24" height="24" rx="4" fill="#1168BD"/>
        <text x="262" y="35" font-family="'Segoe UI', sans-serif" font-size="13" font-weight="600" fill="#222222">Sistema Central (IoBuild)</text>
        <rect x="470" y="18" width="24" height="24" rx="4" fill="#8A9BA8"/>
        <text x="502" y="35" font-family="'Segoe UI', sans-serif" font-size="13" font-weight="600" fill="#222222">Sistema Externo</text>
        <ellipse cx="660" cy="30" rx="12" ry="8" fill="#1F618D"/>
        <text x="680" y="35" font-family="'Segoe UI', sans-serif" font-size="13" font-weight="600" fill="#222222">Cylinder / Pipe</text>
    </g>
</svg>
"""
    return svg

# -------------------------------------------------------------
# 2. DIAGRAMA DE CONTENEDORES (NIVEL 2 - SHAPES WEBBROWSER & MOBILE)
# -------------------------------------------------------------
def build_container_svg():
    width, height = 2400, 1400
    svg = f"""
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" width="{width}" height="{height}" style="background-color:#f8fafd;">
    <defs>
        <filter id="shadow" x="-5%" y="-5%" width="112%" height="116%" filterUnits="userSpaceOnUse">
            <feDropShadow dx="2" dy="5" stdDeviation="5" flood-color="#000000" flood-opacity="0.18"/>
        </filter>
        <filter id="mini-shadow" x="-5%" y="-5%" width="110%" height="120%" filterUnits="userSpaceOnUse">
            <feDropShadow dx="1" dy="2" stdDeviation="2" flood-color="#000000" flood-opacity="0.15"/>
        </filter>
        <marker id="arrowhead" markerWidth="10" markerHeight="7" refX="9" refY="3.5" orient="auto">
            <polygon points="0 0, 10 3.5, 0 7" fill="#666666"/>
        </marker>
    </defs>

    <rect x="50" y="30" width="{width-100}" height="70" rx="8" fill="#ffffff" stroke="#e1e8ed" stroke-width="1.5"/>
    <text x="80" y="66" font-family="'Segoe UI', Roboto, sans-serif" font-size="24" font-weight="700" fill="#1c3d5a">[C4 Nivel 2: Containers] IoBuild Platform - Arquitectura de Contenedores</text>
    <text x="80" y="88" font-family="'Segoe UI', Roboto, sans-serif" font-size="14" fill="#6b7c96">Desglose con shapes oficiales: WebBrowser (SPA y Landing), MobileDevicePortrait (App Móvil), API Monolito, MySQL (Cylinder) y Mosquitto (Pipe)</text>

    <!-- ACTORES (Izquierda) -->
    {person_box(60, 260, 260, 160, "Administrador de Condominios", "Segmento B2B: Administra padrón catastral, liquida gastos comunes y monitorea finanzas.")}
    {person_box(60, 720, 260, 160, "Propietario / Residente", "Segmento B2C: Monitorea telemetría de su unidad, paga cuotas y consulta al copiloto IA.")}

    <!-- BOUNDARY: IoBuild Platform -->
    <g transform="translate(390, 140)">
        <rect x="0" y="0" width="1440" height="1180" rx="18" fill="#ffffff" stroke="#1168BD" stroke-width="2" stroke-dasharray="8,6"/>
        <text x="30" y="40" font-family="'Segoe UI', sans-serif" font-size="20" font-weight="700" fill="#1168BD">IoBuild Platform</text>
        <text x="30" y="62" font-family="'Segoe UI', sans-serif" font-size="13" fill="#6b7c96">[Límite del Sistema de Software]</text>
    </g>

    <!-- CONTENEDORES CON SHAPES OFICIALES -->
    {web_browser_box(450, 240, 250, 150, "Landing Page", "HTML / CSS / JS", "Sitio web público de marketing y captación comercial para empresas administradoras.")}
    {web_browser_box(790, 240, 270, 150, "Web SPA Portal", "Vue 3, Vite, PrimeVue", "Portal web responsivo donde administradores gestionan el catastro y copropietarios pagan cuotas.")}
    {mobile_device_box(790, 490, 270, 160, "Mobile App", "Android, Kotlin", "App móvil nativa para que residentes consulten consumos en vivo y chateen con el copiloto IA.")}

    {container_box(1200, 330, 260, 150, "Nginx (Reverse Proxy)", "Nginx / Alpine", "Expuesto en puerto 8081/80: entrega estáticos de la SPA y redirige peticiones /api al backend.")}

    {container_box(1170, 650, 320, 170, "API Monolito (IoBuild.Api)", "ASP.NET Core 9, C#", "Backend monolítico modular que aloja IAM, Cadastre, Devices, Subscriptions, Proration, Analytics y AiAssistant.")}

    {database_box(920, 970, 290, 180, "MySQL Database", "MySQL 8.0", "Instancia centralizada con volumen persistente para todas las tablas relacionales de IoBuild.")}
    {pipe_box(1360, 980, 300, 160, "Mosquitto MQTT Broker", "Eclipse Mosquitto", "Broker pub/sub para ingesta de lecturas de sensores (telemetry/#) y comandos.")}

    <!-- SISTEMAS EXTERNOS (Derecha) -->
    {system_box(1920, 190, 270, 130, "Stripe Payments", "Pasarela de pagos para planes SaaS B2B y recaudación B2C con tarjeta.", is_external=True)}
    {database_box(1920, 380, 270, 150, "Cloudinary CDN", "Cloud Storage", "Custodia y optimización de comprobantes bancarios y fotos de incidencias.", is_external=True)}
    {system_box(1920, 600, 270, 130, "Google Gemini / OpenAI", "Modelos fundacionales LLM para inferencia RAG y análisis predictivo.", is_external=True)}
    {system_box(1920, 790, 270, 130, "Servicio Notificaciones", "Firebase FCM y correo para alertas críticas de fuga nocturna y recibos.", is_external=True)}
    {system_box(1920, 990, 270, 140, "Medidores y Sensores IoT", "Hardware edge en ductos de agua y tableros generales de energía.", is_external=True)}

    <!-- RELACIONES -->
    {draw_arrow(320, 310, 450, 310, "Visita para conocer", "HTTPS", label_w=120)}
    {draw_arrow(700, 310, 790, 310, "Redirige a login", "HTTPS", label_w=110)}
    {draw_curved_arrow(320, 360, 520, 430, 790, 360, "Ingreso directo a gestión", "HTTPS", label_w=140)}
    {draw_arrow(320, 760, 790, 380, "Usa portal web", "HTTPS", label_w=120)}
    {draw_arrow(320, 790, 790, 560, "Usa app móvil", "Android UI", label_w=120)}

    {draw_arrow(1050, 330, 1200, 380, "Peticiones /api", "HTTPS", label_w=140)}
    {draw_arrow(1050, 540, 1200, 440, "Peticiones /api", "HTTPS", label_w=140)}
    {draw_arrow(1200, 310, 1050, 270, "Sirve archivos SPA", "HTTP", label_w=140)}

    {draw_arrow(1330, 480, 1330, 650, "Proxy inverso a :8080", "HTTP", label_w=160)}

    {draw_arrow(1240, 820, 1080, 970, "Lectura/escritura datos", "MySQL :3306")}
    {draw_arrow(1400, 820, 1480, 980, "Publica/suscribe tópicos", "MQTT :1883")}

    {draw_curved_arrow(1050, 240, 1480, 120, 1920, 230, "Redirige a checkout", "HTTPS / Stripe.js")}
    {draw_arrow(1490, 700, 1920, 260, "Crea sesiones checkout", "HTTPS / REST")}
    {draw_curved_arrow(1920, 280, 1750, 530, 1490, 730, "Webhooks de cobro", "HTTPS")}

    {draw_arrow(1490, 750, 1920, 440, "Almacena comprobantes", "HTTPS / REST")}
    {draw_arrow(1490, 780, 1920, 650, "Inferencia RAG", "HTTPS / REST")}
    {draw_arrow(1490, 810, 1920, 840, "Despacha alertas y recibos", "HTTPS / REST")}

    {draw_arrow(1920, 1050, 1660, 1060, "Publica telemetría sensor", "MQTT :1883")}

    <!-- Leyenda -->
    <g transform="translate(60, 1300)">
        <rect width="1050" height="60" rx="8" fill="#ffffff" stroke="#d0dbe5" stroke-width="1"/>
        <circle cx="28" cy="30" r="10" fill="#08427B"/>
        <text x="46" y="35" font-family="'Segoe UI', sans-serif" font-size="13" font-weight="600" fill="#222222">Persona</text>
        <rect x="120" y="18" width="24" height="24" rx="4" fill="#2A72C9"/>
        <text x="150" y="35" font-family="'Segoe UI', sans-serif" font-size="13" font-weight="600" fill="#222222">WebBrowser / Mobile / Container</text>
        <ellipse cx="400" cy="30" rx="12" ry="8" fill="#1F618D"/>
        <text x="420" y="35" font-family="'Segoe UI', sans-serif" font-size="13" font-weight="600" fill="#222222">Database (Cylinder)</text>
        <rect x="580" y="18" width="30" height="24" rx="6" fill="#1F618D"/>
        <text x="618" y="35" font-family="'Segoe UI', sans-serif" font-size="13" font-weight="600" fill="#222222">MQTT Broker (Pipe)</text>
        <rect x="780" y="18" width="24" height="24" rx="4" fill="#8A9BA8"/>
        <text x="810" y="35" font-family="'Segoe UI', sans-serif" font-size="13" font-weight="600" fill="#222222">Sistema Externo</text>
    </g>
</svg>
"""
    return svg

# -------------------------------------------------------------
# 3. DIAGRAMA DE COMPONENTES (NIVEL 3 - MÓDULOS DE DOMINIO)
# -------------------------------------------------------------
def build_component_svg():
    width, height = 2600, 1500
    svg = f"""
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" width="{width}" height="{height}" style="background-color:#f8fafd;">
    <defs>
        <filter id="shadow" x="-5%" y="-5%" width="112%" height="116%" filterUnits="userSpaceOnUse">
            <feDropShadow dx="2" dy="5" stdDeviation="5" flood-color="#000000" flood-opacity="0.18"/>
        </filter>
        <filter id="mini-shadow" x="-5%" y="-5%" width="110%" height="120%" filterUnits="userSpaceOnUse">
            <feDropShadow dx="1" dy="2" stdDeviation="2" flood-color="#000000" flood-opacity="0.15"/>
        </filter>
        <marker id="arrowhead" markerWidth="10" markerHeight="7" refX="9" refY="3.5" orient="auto">
            <polygon points="0 0, 10 3.5, 0 7" fill="#666666"/>
        </marker>
    </defs>

    <rect x="50" y="30" width="{width-100}" height="70" rx="8" fill="#ffffff" stroke="#e1e8ed" stroke-width="1.5"/>
    <text x="80" y="66" font-family="'Segoe UI', Roboto, sans-serif" font-size="24" font-weight="700" fill="#1c3d5a">[C4 Nivel 3: Components] IoBuild Backend API - Arquitectura Monolítica Modular</text>
    <text x="80" y="88" font-family="'Segoe UI', Roboto, sans-serif" font-size="14" fill="#6b7c96">Desglose de módulos DDD (.NET 9) con shapes oficiales de Componente C4: IAM, Cadastre, Devices, Telemetry, Proration, Subscriptions y AiAssistant</text>

    <!-- ENTRADA DE TRÁFICO (Top): Nginx Reverse Proxy -->
    {container_box(1160, 130, 280, 120, "Nginx (Reverse Proxy)", "Nginx / Alpine", "Enruta el tráfico entrante de Web SPA y Mobile App hacia los controladores y endpoints de cada módulo.")}

    <!-- BOUNDARY: API Monolito (IoBuild.Api) -->
    <g transform="translate(80, 310)">
        <rect x="0" y="0" width="2440" height="740" rx="18" fill="#ffffff" stroke="#2A72C9" stroke-width="2.5" stroke-dasharray="9,6"/>
        <text x="30" y="36" font-family="'Segoe UI', sans-serif" font-size="20" font-weight="700" fill="#2A72C9">API Monolito (IoBuild.Api)</text>
        <text x="30" y="58" font-family="'Segoe UI', sans-serif" font-size="13" fill="#6b7c96">[Límite del Contenedor Backend en ASP.NET Core 9 / C#]</text>
    </g>

    <!-- FILA DE LOS 7 MÓDULOS DDD CON SHAPE COMPONENT -->
    {component_box(120, 420, 310, 180, "Módulo IAM", "ASP.NET Core", "Autenticación, hashing BCrypt, emisión de tokens JWT con claims catastrales y control de accesos.")}
    {component_box(460, 420, 310, 180, "Módulo Cadastre & Units", "ASP.NET Core", "Catastro físico de edificios, torres y departamentos, y padrón oficial de alícuotas de copropiedad.")}
    {component_box(800, 420, 310, 180, "Módulo IoT Devices", "ASP.NET Core", "Registro de medidores de agua/luz, comisionamiento y cliente de comunicación MQTT.")}
    {component_box(1140, 420, 310, 180, "Módulo Telemetry & Analytics", "ASP.NET Core", "Ingesta de telemetría, agregaciones horarias de m³ y kWh, y detección de patrones de fuga continua.")}
    {component_box(1480, 420, 310, 180, "Módulo Proration (Core)", "ASP.NET Core", "Motor matemático de distribución de gastos comunes según alícuotas y liquidación de cuotas.")}
    {component_box(1820, 420, 310, 180, "Módulo Subscriptions", "ASP.NET Core", "Planes SaaS B2B, facturación HaaS por medidor instalado y procesamiento de webhooks Stripe.")}
    {component_box(2160, 420, 310, 180, "Módulo AiAssistant (RAG)", "ASP.NET Core", "Copiloto conversacional: inyección de contexto RAG de consumos y diagnósticos con Gemini/OpenAI.")}

    <!-- COMPONENTE TRANSVERSAL DE PERSISTENCIA (Y=760) -->
    {component_box(920, 770, 760, 160, "Módulo Persistence (IoBuildDbContext)", "Entity Framework Core 9", "Unit of Work centralizado que gestiona DbSets, mapeos de entidades, transacciones ACID y migraciones relacionales.")}

    <!-- DEPENDENCIAS EXTERNAS E INFRAESTRUCTURA (Abajo) -->
    {pipe_box(300, 1180, 340, 140, "Mosquitto MQTT Broker", "Eclipse Mosquitto", "Intercambia mensajes MQTT de telemetría de medidores y comandos en tiempo real.")}
    {database_box(1080, 1160, 440, 180, "MySQL Database", "MySQL 8.0", "Instancia de base de datos relacional para usuarios, catastro, recibos, cuotas y telemetría consolidada.")}
    {system_box(1660, 1180, 280, 140, "Stripe Payments API", "Procesa cargos recurrentes SaaS y transacciones de recibos con tarjeta.", is_external=True)}
    {database_box(2000, 1170, 280, 150, "Cloudinary CDN", "Cloud Media", "Almacenamiento y entrega de comprobantes bancarios y fotos de incidencias.", is_external=True)}
    {system_box(2320, 1180, 250, 140, "Google Gemini API", "Inferencia de modelos fundacionales RAG para explicaciones de cobro.", is_external=True)}

    <!-- RELACIONES DESDE NGINX -->
    {draw_arrow(1200, 250, 270, 420, "Enruta /auth", "HTTP", label_w=120)}
    {draw_arrow(1240, 250, 610, 420, "Enruta /cadastre", "HTTP", label_w=120)}
    {draw_arrow(1270, 250, 950, 420, "Enruta /devices", "HTTP", label_w=120)}
    {draw_arrow(1300, 250, 1290, 420, "Enruta /telemetry", "HTTP", label_w=130)}
    {draw_arrow(1330, 250, 1630, 420, "Enruta /proration", "HTTP", label_w=130)}
    {draw_arrow(1360, 250, 1970, 420, "Enruta /subscriptions", "HTTP", label_w=140)}
    {draw_arrow(1400, 250, 2300, 420, "Enruta /ai-chat", "HTTP", label_w=120)}

    <!-- RELACIONES A PERSISTENCIA -->
    {draw_arrow(270, 600, 950, 770, "Guarda usuarios")}
    {draw_arrow(610, 600, 1050, 770, "Guarda edificios y alícuotas")}
    {draw_arrow(950, 600, 1150, 770, "Guarda medidores")}
    {draw_arrow(1290, 600, 1300, 770, "Guarda consumos")}
    {draw_arrow(1630, 600, 1450, 770, "Guarda liquidaciones y cuotas")}
    {draw_arrow(1970, 600, 1550, 770, "Guarda planes y facturación")}

    <!-- PERSISTENCIA A MYSQL -->
    {draw_arrow(1300, 930, 1300, 1160, "Ejecuta queries y transacciones SQL", "MySQL Protocol :3306", label_w=240)}

    <!-- CONEXIONES A SISTEMAS EXTERNOS -->
    {draw_arrow(950, 600, 470, 1180, "Suscripción y comandos", "MQTT :1883", label_w=170)}
    {draw_arrow(1970, 600, 1800, 1180, "Sesiones checkout y webhooks", "HTTPS / REST", label_w=190)}
    {draw_arrow(2300, 600, 2140, 1170, "Guarda fotos de perfil/vouchers", "HTTPS / REST", label_w=190)}
    {draw_arrow(2310, 600, 2440, 1180, "Prompts RAG multimodales", "HTTPS / REST", label_w=180)}

    <!-- Leyenda -->
    <g transform="translate(60, 1380)">
        <rect width="950" height="60" rx="8" fill="#ffffff" stroke="#d0dbe5" stroke-width="1"/>
        <rect x="20" y="18" width="24" height="24" rx="4" fill="#438DD5"/>
        <text x="52" y="35" font-family="'Segoe UI', sans-serif" font-size="13" font-weight="600" fill="#222222">Componente de Módulo (.NET 9)</text>
        <rect x="300" y="18" width="24" height="24" rx="4" fill="#2A72C9"/>
        <text x="332" y="35" font-family="'Segoe UI', sans-serif" font-size="13" font-weight="600" fill="#222222">Contenedor Nginx</text>
        <ellipse cx="530" cy="30" rx="12" ry="8" fill="#1F618D"/>
        <text x="552" y="35" font-family="'Segoe UI', sans-serif" font-size="13" font-weight="600" fill="#222222">Base de Datos (Cylinder)</text>
        <rect x="740" y="18" width="24" height="24" rx="4" fill="#8A9BA8"/>
        <text x="772" y="35" font-family="'Segoe UI', sans-serif" font-size="13" font-weight="600" fill="#222222">Sistema Externo</text>
    </g>
</svg>
"""
    return svg

# -------------------------------------------------------------
# 4. DIAGRAMA DE DESPLIEGUE (NIVEL 4 - INFRAESTRUCTURA REAL)
# -------------------------------------------------------------
def build_deployment_svg():
    width, height = 2400, 1450
    svg = f"""
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" width="{width}" height="{height}" style="background-color:#f8fafd;">
    <defs>
        <filter id="shadow" x="-5%" y="-5%" width="112%" height="116%" filterUnits="userSpaceOnUse">
            <feDropShadow dx="2" dy="5" stdDeviation="5" flood-color="#000000" flood-opacity="0.18"/>
        </filter>
        <filter id="mini-shadow" x="-5%" y="-5%" width="110%" height="120%" filterUnits="userSpaceOnUse">
            <feDropShadow dx="1" dy="2" stdDeviation="2" flood-color="#000000" flood-opacity="0.15"/>
        </filter>
        <marker id="arrowhead" markerWidth="10" markerHeight="7" refX="9" refY="3.5" orient="auto">
            <polygon points="0 0, 10 3.5, 0 7" fill="#666666"/>
        </marker>
    </defs>

    <rect x="50" y="30" width="{width-100}" height="70" rx="8" fill="#ffffff" stroke="#e1e8ed" stroke-width="1.5"/>
    <text x="80" y="66" font-family="'Segoe UI', Roboto, sans-serif" font-size="24" font-weight="700" fill="#1c3d5a">[C4 Nivel 4: Deployment] IoBuild Platform - Diagrama de Despliegue en Producción</text>
    <text x="80" y="88" font-family="'Segoe UI', Roboto, sans-serif" font-size="14" fill="#6b7c96">Mapeo de contenedores Docker (Nginx, Vue SPA, ASP.NET Core 9, MySQL 8, Mosquitto), dispositivos cliente y servicios Cloud</text>

    <!-- NODO 1: HARDWARE EDGE (Izquierda Arriba) -->
    {deployment_node_box(60, 140, 480, 360, "Instalación Física: Condominio", "Hardware Edge", "Ductos sanitarios y tableros generales de submedición")}
    {system_box(100, 240, 400, 180, "Medidores Inteligentes IoT", "Medidores ultrasónicos de agua y analizadores de red eléctrica transmitiendo lecturas por microcontrolador ESP32.")}

    <!-- NODO 2: DISPOSITIVOS DE CLIENTES (Izquierda Abajo) -->
    {deployment_node_box(60, 560, 480, 780, "Dispositivos de Usuario Final", "Client Devices", "Dispositivos de administradores y copropietarios")}
    {deployment_node_box(90, 640, 420, 320, "PC / Laptop de Administrador", "Windows / macOS", "Navegador web para gestión de edificios y cobranzas")}
    {web_browser_box(120, 730, 360, 200, "Chrome / Edge Browser", "Web Browser", "Ejecuta la Web SPA de IoBuild en Vue 3 para configurar catastro y liquidaciones.")}

    {deployment_node_box(90, 990, 420, 320, "Smartphone de Residente", "Android 12+", "Dispositivo móvil para residentes en copropiedad")}
    {mobile_device_box(120, 1070, 360, 210, "IoBuild Mobile App", "Kotlin / Jetpack Compose", "App móvil nativa para visualización de telemetría y chat con el asistente IA.")}

    <!-- NODO 3: SERVIDOR VPS DOKPLOY / DOCKER (Centro) -->
    {deployment_node_box(580, 140, 1260, 1200, "Servidor de Producción VPS", "Ubuntu Linux 24.04 LTS (Dokploy Cloud Host)", "Infraestructura cloud con Docker Engine y red aislada iobuild-network")}

    <!-- RED INTERNA DOCKER -->
    <g transform="translate(610, 230)">
        <rect x="0" y="0" width="1200" height="1080" rx="14" fill="#f1f5f9" stroke="#0284c7" stroke-width="2" stroke-dasharray="8,5"/>
        <text x="24" y="32" font-family="'Segoe UI', sans-serif" font-size="16" font-weight="700" fill="#0369a1">Docker Host Environment <tspan font-weight="400" font-size="13" fill="#64748b">[Red Virtual: iobuild-network]</tspan></text>
    </g>

    <!-- CONTENEDORES DOCKER -->
    {deployment_node_box(640, 300, 520, 260, "Contenedor: iobuild-nginx", "Docker: nginx:alpine", "Proxy inverso y balanceador HTTP expuesto")}
    {container_box(680, 380, 440, 150, "Nginx Reverse Proxy", "Nginx", "Expone puertos 80:80 y 8081:80, entrega estáticos y delega tráfico /api.")}

    {deployment_node_box(640, 600, 520, 260, "Contenedor: iobuild-frontend", "Docker: nginx:alpine", "Servidor estático de la Web SPA compilada")}
    {web_browser_box(680, 680, 440, 150, "Frontend SPA Dist", "Vue 3 / Vite", "Archivos HTML/JS/CSS optimizados de la plataforma web de administración.")}

    {deployment_node_box(1220, 300, 560, 340, "Contenedor: iobuild-api", "Docker: mcr.microsoft.com/dotnet/aspnet:9.0", "Backend monolítico modular")}
    {container_box(1260, 380, 480, 220, "IoBuild.Api (.NET 9 Web API)", "ASP.NET Core 9 / C#", "Aloja los 7 módulos de dominio, Entity Framework Core 9, middleware JWT, adaptadores de pasarela y MQTT.")}

    {deployment_node_box(1220, 680, 560, 310, "Contenedor: mysql-monolith", "Docker: mysql:8.0", "Base de datos relacional persistente")}
    {database_box(1260, 760, 480, 200, "MySQL Database Instance", "MySQL 8.0", "Servidor de base de datos relacional con volumen persistente montado en mysql_monolith_data.")}

    {deployment_node_box(640, 920, 520, 340, "Contenedor: iobuild-mosquitto", "Docker: eclipse-mosquitto:2", "Broker MQTT para telemetría")}
    {pipe_box(680, 1020, 440, 190, "Mosquitto Broker", "MQTT :1883", "Broker de mensajería ligera para ingesta de telemetría y suscripción de dispositivos.")}

    <!-- NODO 4: SERVICIOS CLOUD DE TERCEROS (Derecha) -->
    {deployment_node_box(1880, 140, 480, 1200, "Servicios Cloud Externos (PaaS / SaaS)", "Terceros Gestionados", "Infraestructura externa de alta disponibilidad")}
    {system_box(1920, 240, 400, 160, "Stripe Payments Cloud", "Pasarela PCI-DSS para cobro de suscripciones SaaS y cuotas con tarjeta bancaria.", is_external=True)}
    {database_box(1920, 460, 400, 170, "Cloudinary Media Cloud", "CDN / Object Storage", "Almacenamiento y entrega de comprobantes bancarios escaneados y fotos.", is_external=True)}
    {system_box(1920, 700, 400, 160, "Google Gemini / OpenAI Platform", "Modelos fundacionales multimodales para análisis RAG e inferencias conversacionales.", is_external=True)}
    {system_box(1920, 940, 400, 160, "Firebase Cloud Messaging (FCM)", "Infraestructura de notificaciones push móviles para alertas críticas de fuga.", is_external=True)}

    <!-- RELACIONES DE DESPLIEGUE -->
    <!-- Medidores -> Mosquitto -->
    {draw_arrow(500, 330, 680, 1080, "Transmite telemetría", "MQTT :1883", label_w=160)}

    <!-- Browsers / Apps -> Nginx -->
    {draw_arrow(480, 830, 680, 450, "Peticiones HTTPS :8081", "HTTPS / TCP", label_w=160)}
    {draw_arrow(480, 1170, 680, 480, "Peticiones /api", "HTTPS / TCP", label_w=150)}

    <!-- Nginx -> Frontend / API -->
    {draw_arrow(900, 530, 900, 680, "Sirve archivos estáticos", "Docker Bridge", label_w=170)}
    {draw_arrow(1120, 450, 1260, 450, "Proxy inverso :8080", "HTTP / TCP", label_w=160)}

    <!-- API -> MySQL / Mosquitto -->
    {draw_arrow(1500, 600, 1500, 760, "Persistencia relacional", "TCP :3306", label_w=160)}
    {draw_arrow(1260, 520, 1120, 1080, "Suscripción a telemetría", "MQTT :1883", label_w=170)}

    <!-- API -> Cloud -->
    {draw_arrow(1740, 420, 1920, 320, "Sesiones y cobros", "REST / HTTPS", label_w=150)}
    {draw_arrow(1740, 460, 1920, 540, "Subida de vouchers", "REST / HTTPS", label_w=150)}
    {draw_arrow(1740, 500, 1920, 780, "Prompts RAG", "REST / HTTPS", label_w=150)}
    {draw_arrow(1740, 540, 1920, 1020, "Notificaciones push", "REST / HTTPS", label_w=160)}

    <!-- Leyenda -->
    <g transform="translate(60, 1370)">
        <rect width="1050" height="55" rx="8" fill="#ffffff" stroke="#d0dbe5" stroke-width="1"/>
        <rect x="20" y="16" width="24" height="24" rx="4" fill="#ffffff" stroke="#94a3b8" stroke-width="2" stroke-dasharray="4,3"/>
        <text x="52" y="33" font-family="'Segoe UI', sans-serif" font-size="13" font-weight="600" fill="#222222">Nodo de Despliegue (Infraestructura / Host)</text>
        <rect x="360" y="16" width="24" height="24" rx="4" fill="#2A72C9"/>
        <text x="392" y="33" font-family="'Segoe UI', sans-serif" font-size="13" font-weight="600" fill="#222222">Contenedor de Software (Docker)</text>
        <ellipse cx="680" cy="28" rx="12" ry="8" fill="#1F618D"/>
        <text x="702" y="33" font-family="'Segoe UI', sans-serif" font-size="13" font-weight="600" fill="#222222">Base de Datos / Broker</text>
        <rect x="880" y="16" width="24" height="24" rx="4" fill="#8A9BA8"/>
        <text x="912" y="33" font-family="'Segoe UI', sans-serif" font-size="13" font-weight="600" fill="#222222">Servicio Cloud</text>
    </g>
</svg>
"""
    return svg

def render_svg_and_png(svg_content, html_filename, png_filename, jpg_filename, window_w, window_h):
    html_path = os.path.join(DOCS_DIAGRAMS_DIR, html_filename)
    png_path = os.path.join(DOCS_DIAGRAMS_DIR, png_filename)
    jpg_path = os.path.join(DOCS_DIAGRAMS_DIR, jpg_filename)
    asset_png_path = os.path.join(ASSETS_DIR, png_filename)

    html = f"""<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8"/>
    <style>
        body {{ margin:0; padding:0; background:#f8fafd; display:flex; justify-content:center; align-items:center; }}
    </style>
</head>
<body>
    {svg_content}
</body>
</html>"""
    with open(html_path, "w", encoding="utf-8") as f:
        f.write(html)
        
    cmd = [
        EDGE_PATH,
        "--headless=new",
        "--disable-gpu",
        f"--window-size={window_w},{window_h}",
        f"--screenshot={png_path}",
        f"file:///{html_path.replace(os.sep, '/')}"
    ]
    subprocess.run(cmd, check=True)
    
    if os.path.exists(png_path):
        with Image.open(png_path) as img:
            rgb_img = img.convert("RGB")
            rgb_img.save(jpg_path, "JPEG", quality=95, optimize=True)
        # Also copy to assets
        shutil.copyfile(png_path, asset_png_path)
        print(f"Generated: {png_path} and copied to {asset_png_path}")

if __name__ == "__main__":
    print("1. Rendering Nivel 1: Contexto...")
    render_svg_and_png(build_context_svg(), "c4_context.html", "c4_nivel1_contexto.png", "c4_nivel1_contexto.jpg", 1850, 1150)
    
    print("2. Rendering Nivel 2: Contenedores...")
    render_svg_and_png(build_container_svg(), "c4_container.html", "c4_nivel2_contenedores.png", "c4_nivel2_contenedores.jpg", 2450, 1450)
    
    print("3. Rendering Nivel 3: Componentes...")
    render_svg_and_png(build_component_svg(), "c4_component.html", "c4_nivel3_componentes.png", "c4_nivel3_componentes.jpg", 2650, 1550)

    print("4. Rendering Nivel 4: Despliegue...")
    render_svg_and_png(build_deployment_svg(), "c4_deployment.html", "c4_nivel4_despliegue.png", "c4_nivel4_despliegue.jpg", 2450, 1500)
    print("All 4 C4 Diagrams rendered with official Structurizr shapes successfully!")
