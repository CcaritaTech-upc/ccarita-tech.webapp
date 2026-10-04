Feature: US09 Ver Plan de Suscripción Actual
  Como ingeniero
  quiero ver mi plan de suscripción actual y su estado
  para confirmar los beneficios que tengo y el costo mensual.

  Scenario Outline: Consulta de suscripción activa y beneficios del plan
    Given que el ingeniero <correo> cuenta con una suscripción vigente
    When accede a la sección "Mi Suscripción y Pagos"
    Then el sistema mostrará el plan contratado <nombre_plan>, costo mensual <costo> y estado <estado_suscripcion>
    And listará los beneficios incluidos de analíticas y soporte
    Examples:
      | correo | nombre_plan | costo | estado_suscripcion |
      | builder@iobuild.pe | Enterprise | $199.00 | Active |
      | pro@iobuild.pe | Professional | $89.99 | Active |

  Scenario Outline: Usuario sin suscripción activa visualiza catálogo de planes
    Given que el usuario no posee una suscripción activa
    When accede al módulo de suscripciones
    Then el sistema mostrará el catálogo de planes disponibles para compra con opciones Starter, Pro y Enterprise
    And habilitará el botón de checkout seguro a través de Stripe
    Examples:
      | correo |
      | nuevo@iobuild.pe |
