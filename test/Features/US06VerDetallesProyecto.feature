@US06
Feature: US06 Ver detalles de un proyecto
  Como arquitecto
  quiero ver los detalles de un proyecto específico
  para revisar su información y la estructura de pisos y unidades.

  Scenario Outline: Consulta exitosa de detalles de proyecto existente
    Given el constructor con alias <alias> tiene el proyecto "<nombre>" en "<ubicacion>" con estructura de un piso y una unidad
    When abre los detalles del proyecto
    Then la vista mostrará "<nombre>" y la ubicación "<ubicacion>"
    And mostrará la unidad creada
    Examples:
      | alias | nombre | ubicacion |
      | torre | Torre Sky | Lima |
      | parque | Residencial Park | Cayma, Arequipa |

  Scenario: Intento de consulta de proyecto inexistente
    Given el constructor con alias proyecto404 tiene una sesión activa
    When solicita por API los detalles del proyecto inexistente 999999
    Then la API de proyectos responderá con código 404 Not Found
