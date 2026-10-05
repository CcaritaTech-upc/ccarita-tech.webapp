@US09
Feature: US09 Ver Plan de Suscripción Actual
  Como constructor
  quiero ver mi plan de suscripción actual y los planes disponibles
  para confirmar los beneficios y el costo mensual.

  Scenario Outline: Consulta de suscripción activa y precio del plan
    Given el constructor con alias <alias> tiene una suscripción activa al plan <plan_id>
    When accede a la sección "Mi Suscripción y Pagos"
    Then verá el plan contratado "<nombre_plan>" con costo mensual "<costo>"
    And el estado de suscripción será "Activo"
    Examples:
      | alias | plan_id | nombre_plan | costo |
      | starter | 1 | Starter | $299 |
      | enterprise | 3 | Enterprise | $1299 |

  Scenario: Usuario sin suscripción activa visualiza el catálogo de planes
    Given el constructor con alias nuevosinplan no tiene una suscripción activa
    When accede al módulo de suscripciones
    Then verá los planes Starter, Professional y Enterprise disponibles
    And podrá seleccionar una opción de checkout por cada plan
