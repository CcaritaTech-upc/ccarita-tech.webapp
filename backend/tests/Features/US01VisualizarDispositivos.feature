Feature: US01 Visualizar los dispositivos y su distribución por tipo
  Como ingeniero
  quiero ver cuáles son los dispositivos y cómo están distribuidos por tipo
  para realizar un análisis más detallado de los recursos disponibles.

  Scenario Outline: Consulta los dispositivos conectados en un proyecto
    Given el <ingeniero> accede a la sección de dispositivos del proyecto con ID <proyecto_id>
    When consulta la lista de dispositivos conectados
    Then el sistema mostrará una lista detallada de dispositivos clasificados por su <tipo>
    And el estado de conexión visible será <estado>
    Examples:
      | ingeniero | proyecto_id | tipo | estado |
      | fabrizio@iobuild.pe | 1 | SmartMeter | Online |
      | axel@iobuild.pe | 2 | LightingController | Online |

  Scenario Outline: Filtro de dispositivos por tipo específico
    Given el <ingeniero> se encuentra visualizando el catálogo de dispositivos
    When selecciona el filtro por tipo <tipo_filtro>
    Then el sistema mostrará únicamente los dispositivos cuyo tipo coincida con <tipo_filtro>
    And el contador total de dispositivos reflejará <cantidad_esperada>
    Examples:
      | ingeniero | tipo_filtro | cantidad_esperada |
      | fabrizio@iobuild.pe | SmartMeter | 8 |
      | mateo@iobuild.pe | SensorTemperatura | 15 |

  Scenario Outline: Consulta en proyecto sin dispositivos registrados
    Given el <ingeniero> accede a un proyecto nuevo sin dispositivos con ID <proyecto_id>
    When consulta la lista de dispositivos
    Then el sistema mostrará el mensaje <mensaje_alerta>
    And la distribución por tipo permanecerá vacía
    Examples:
      | ingeniero | proyecto_id | mensaje_alerta |
      | fabrizio@iobuild.pe | 99 | No hay dispositivos registrados en este proyecto |
