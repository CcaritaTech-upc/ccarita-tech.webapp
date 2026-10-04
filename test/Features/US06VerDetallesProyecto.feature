Feature: US06 Ver detalles de un proyecto
  Como arquitecto
  quiero ver los detalles de un proyecto específico
  para poder revisar su información completa, estructura de pisos y unidades.

  Scenario Outline: Consulta exitosa de detalles de proyecto existente
    Given que existe un proyecto registrado con ID <proyecto_id>
    When el arquitecto selecciona la opción "Ver Detalles" del proyecto
    Then el sistema cargará la información completa mostrando nombre <nombre>, ubicación <ubicacion> y unidades registradas <unidades>
    And mostrará el desglose de estructura de pisos
    Examples:
      | proyecto_id | nombre | ubicacion | unidades |
      | 1 | Torre Sky | Lima | 40 |
      | 2 | Residencial Park | Arequipa | 18 |

  Scenario Outline: Intento de consulta de proyecto inexistente
    Given el usuario solicita los detalles de un proyecto con ID <proyecto_inexistente>
    When el cliente envía la petición HTTP GET /api/v1/projects/<proyecto_inexistente>
    Then el sistema responderá con código de estado 404 Not Found
    And mostrará la vista de recurso no disponible con mensaje <mensaje_error>
    Examples:
      | proyecto_inexistente | mensaje_error |
      | 9999 | El proyecto solicitado no existe |
