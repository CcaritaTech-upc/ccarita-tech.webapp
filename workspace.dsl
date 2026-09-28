workspace "IoBuild Platform" "Diagramas de Arquitectura C4 para IoBuild Platform - Gestión Inteligente y Prorrateo de Gastos Comunes" {

    model {
        # Segmentos de Clientes Objetivo (Únicos 2 segmentos)
        admin = person "Administrador de Condominios" "Segmento Objetivo B2B: Property Manager que configura catastro, valida alícuotas, aprueba liquidaciones y supervisa cobros." "Person"
        owner = person "Propietario / Residente" "Segmento Objetivo B2C: Copropietario que visualiza consumos en tiempo real, abona cuotas con tarjeta y consulta al asistente IA." "Person"

        # Sistemas Externos Reales (Estrictamente los 3 especificados en el proyecto)
        stripe = softwareSystem "Stripe Payments Platform" "Pasarela de pagos para planes de suscripción B2B y recaudación B2C con tarjeta." "External System"
        cloudinary = softwareSystem "Cloudinary CDN" "Custodia y optimización de comprobantes bancarios escaneados y fotos de incidencias." "External Storage"
        gemini = softwareSystem "Google Gemini / OpenAI API" "Modelos fundacionales LLM para análisis RAG, soporte en lenguaje natural y mantenimiento predictivo." "External System"

        # Sistema Central IoBuild
        iobuild = softwareSystem "IoBuild Platform" "Plataforma central SaaS/HaaS para gestión de condominios: cálculo de alícuotas, liquidación matemática de gastos comunes, pasarela de cobros y copiloto cognitivo RAG." "MainSystem" {
            landingPage = container "Landing Page" "Sitio web público de marketing y captación comercial para empresas administradoras." "HTML / CSS / JS" "WebBrowser"
            webApp = container "Web SPA Portal" "Portal web responsivo donde administradores gestionan el catastro y copropietarios pagan cuotas." "Vue 3, Vite, PrimeVue" "WebBrowser"
            mobileApp = container "Mobile App" "App móvil nativa para que residentes consulten consumos en vivo y chateen con el copiloto IA." "Android, Kotlin" "MobileDevicePortrait"
            nginx = container "Nginx (Reverse Proxy)" "Expuesto en puerto 8081/80: entrega estáticos de la SPA y redirige peticiones /api al backend." "Nginx / Alpine" "Proxy"

            api = container "API Monolito (IoBuild.Api)" "Backend monolítico modular que aloja IAM, Cadastre, Subscriptions, Proration, Telemetry y AiAssistant." "ASP.NET Core 9, C#" "Backend" {
                iamModule = component "Módulo IAM" "Autenticación, hashing BCrypt, emisión de tokens JWT con claims catastrales y control de accesos." "ASP.NET Core" "Component"
                cadastreModule = component "Módulo Cadastre & Units" "Catastro físico de edificios, torres y departamentos, y padrón oficial de alícuotas de copropiedad." "ASP.NET Core" "Component"
                devModule = component "Módulo Units & Devices" "Asociación y registro de identificadores de medidores por departamento y configuración de lectura." "ASP.NET Core" "Component"
                telemetryModule = component "Módulo Telemetry & Analytics" "Procesamiento de lecturas, agregaciones horarias de m³ y kWh, y detección de patrones de consumo anómalo." "ASP.NET Core" "Component"
                prorationModule = component "Módulo Proration (Core)" "Motor matemático de distribución de gastos comunes según alícuotas y liquidación de cuotas." "ASP.NET Core" "Component"
                subModule = component "Módulo Subscriptions" "Planes SaaS B2B, facturación de membresías y procesamiento de webhooks Stripe." "ASP.NET Core" "Component"
                aiModule = component "Módulo AiAssistant (RAG)" "Copiloto conversacional: inyección de contexto RAG de consumos y diagnósticos con Gemini/OpenAI." "ASP.NET Core" "Component"
                persistenceModule = component "Módulo Persistence (IoBuildDbContext)" "Unit of Work centralizado que gestiona DbSets, mapeos de entidades, transacciones ACID y migraciones relacionales." "Entity Framework Core 9" "Component"
            }

            mosquitto = container "Mosquitto (MQTT Broker)" "Broker pub/sub para ingesta asíncrona de telemetría y eventos de consumo." "Eclipse Mosquitto" "Pipe"
            database = container "MySQL Database" "Instancia centralizada con volumen persistente para todas las tablas relacionales de IoBuild." "MySQL 8.0" "Database"
        }

        # Relaciones de Personas con Sistema Central (Nivel 1)
        admin -> iobuild "Gestiona catastro y liquidaciones" "HTTPS / Web Portal"
        owner -> iobuild "Visualiza consumos y paga cuotas" "HTTPS / Mobile App y Web"

        # Relaciones del Sistema Central con Externos (Nivel 1)
        iobuild -> stripe "Procesa cargos de suscripción y recibos" "REST / HTTPS"
        iobuild -> cloudinary "Almacena y optimiza vouchers y fotos" "REST / HTTPS"
        iobuild -> gemini "Inyecta contexto RAG y recibe inferencias" "REST / HTTPS"

        # Relaciones a Nivel Contenedores (Nivel 2)
        admin -> landingPage "Visita para conocer plataforma" "HTTPS"
        landingPage -> webApp "Redirige a portal" "HTTPS"
        admin -> webApp "Ingreso a gestión y administración" "HTTPS"
        owner -> webApp "Consulta estados de cuenta y pagos" "HTTPS"
        owner -> mobileApp "Consulta consumos y chat IA" "Android UI"

        webApp -> nginx "Peticiones /api" "HTTPS"
        mobileApp -> nginx "Peticiones /api" "HTTPS"
        nginx -> webApp "Sirve archivos estáticos SPA" "HTTP"
        nginx -> api "Proxy inverso a :8080" "HTTP"

        api -> database "Lectura y persistencia relacional" "MySQL :3306"
        api -> mosquitto "Suscripción a telemetría y eventos" "MQTT :1883"

        webApp -> stripe "Redirige a checkout" "HTTPS / Stripe.js"
        api -> stripe "Crea sesiones checkout y recibe webhooks" "HTTPS / REST"
        api -> cloudinary "Guarda fotos de perfil y comprobantes" "HTTPS / REST"
        api -> gemini "Inferencia LLM y RAG" "HTTPS / REST"

        # Relaciones a Nivel Componentes (Nivel 3)
        nginx -> iamModule "Enruta /api/v1/auth" "HTTP"
        nginx -> cadastreModule "Enruta /api/v1/cadastre" "HTTP"
        nginx -> devModule "Enruta /api/v1/devices" "HTTP"
        nginx -> telemetryModule "Enruta /api/v1/telemetry" "HTTP"
        nginx -> prorationModule "Enruta /api/v1/proration" "HTTP"
        nginx -> subModule "Enruta /api/v1/subscriptions" "HTTP"
        nginx -> aiModule "Enruta /api/v1/ai-chat" "HTTP"

        iamModule -> persistenceModule "Gestiona usuarios y credenciales"
        cadastreModule -> persistenceModule "Gestiona edificios, unidades y alícuotas"
        devModule -> persistenceModule "Gestiona medidores y asignación"
        telemetryModule -> persistenceModule "Gestiona histórico y lecturas horarias"
        prorationModule -> persistenceModule "Gestiona liquidaciones y cuotas"
        subModule -> persistenceModule "Gestiona planes y suscripciones"

        persistenceModule -> database "Ejecuta queries y transacciones ACID" "MySQL Protocol :3306"

        telemetryModule -> mosquitto "Consume eventos de telemetría" "MQTT :1883"
        subModule -> stripe "Gestiona planes y valida pagos" "HTTPS / REST"
        aiModule -> gemini "Consulta prompts multimodales y RAG" "HTTPS / REST"
        aiModule -> persistenceModule "Consulta histórico para RAG"

        # Entorno de Despliegue en Producción (Nivel 4)
        deploymentEnvironment "Production" {
            clientNode = deploymentNode "Dispositivos de Usuario Final" "Equipos de usuarios administradores y copropietarios" "Client Devices" {
                browserNode = deploymentNode "PC / Laptop de Administrador" "Navegador web para configuración y gestión" "Windows / macOS" {
                    containerInstance webApp
                }
                phoneNode = deploymentNode "Smartphone de Residente" "Dispositivo móvil para copropietarios" "Android 12+" {
                    containerInstance mobileApp
                }
            }

            vpsNode = deploymentNode "Servidor de Producción VPS" "Infraestructura cloud con Docker Engine (Dokploy / Ubuntu 24.04 LTS)" "Ubuntu Linux 24.04 LTS" {
                dockerHost = deploymentNode "Docker Host Environment" "Red aislada docker iobuild-network" "Docker Engine" {
                    nginxInstance = containerInstance nginx
                    frontendInstance = containerInstance landingPage
                    apiInstance = containerInstance api
                    dbInstance = containerInstance database
                    brokerInstance = containerInstance mosquitto
                }
            }

            cloudServices = deploymentNode "Servicios Cloud Externos" "Infraestructura externa gestionada de alta disponibilidad (PaaS / SaaS)" "Multi-Cloud" {
                softwareSystemInstance stripe
                softwareSystemInstance cloudinary
                softwareSystemInstance gemini
            }
        }
    }

    views {
        systemContext iobuild "SystemContext" "Nivel 1: Contexto del Sistema IoBuild Platform" {
            include *
            autoLayout lr
        }

        container iobuild "Containers" "Nivel 2: Contenedores del Sistema IoBuild Platform" {
            include *
            autoLayout lr
        }

        component api "Components" "Nivel 3: Componentes del API Monolito (IoBuild.Api)" {
            include *
            include nginx
            include database
            include mosquitto
            include stripe
            include cloudinary
            include gemini
            autoLayout tb
        }

        deployment iobuild "Production" "Deployment" "Nivel 4: Diagrama de Despliegue en Producción" {
            include *
            autoLayout tb
        }

        styles {
            element "Person" {
                shape Person
                background #08427B
                color #ffffff
            }
            element "MainSystem" {
                shape RoundedBox
                background #1168BD
                color #ffffff
            }
            element "External System" {
                shape RoundedBox
                background #8A9BA8
                color #ffffff
            }
            element "External Storage" {
                shape Cylinder
                background #7f8c8d
                color #ffffff
            }
            element "WebBrowser" {
                shape WebBrowser
                background #2A72C9
                color #ffffff
            }
            element "MobileDevicePortrait" {
                shape MobileDevicePortrait
                background #2A72C9
                color #ffffff
            }
            element "Proxy" {
                shape Box
                background #2A72C9
                color #ffffff
            }
            element "Backend" {
                shape Box
                background #2A72C9
                color #ffffff
            }
            element "Component" {
                shape Component
                background #438DD5
                color #ffffff
            }
            element "Database" {
                shape Cylinder
                background #1F618D
                color #ffffff
            }
            element "Pipe" {
                shape Pipe
                background #1F618D
                color #ffffff
            }
        }
    }
}
