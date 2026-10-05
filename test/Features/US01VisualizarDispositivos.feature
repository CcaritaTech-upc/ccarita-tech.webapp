@US01
Feature: US01 Visualizar los dispositivos y su distribución por tipo
  Como constructor
  quiero ver la distribución de los dispositivos de mis proyectos
  para analizar los recursos disponibles.

  Scenario: El dashboard muestra la distribución por tipo del proyecto
    Given el constructor con alias dispositivos tiene un proyecto con estructura de un piso y una unidad
    When abre el dashboard del constructor
    Then la distribución mostrará los cinco tipos de dispositivos de la estructura

  Scenario: El constructor consulta el estado de los dispositivos del proyecto
    Given el constructor con alias listadispositivos tiene un proyecto con estructura de un piso y una unidad
    When abre la administración de dispositivos
    Then la tabla mostrará los dispositivos de piso y unidad con su estado de conexión

  Scenario: El proyecto sin estructura todavía no tiene dispositivos por tipo
    Given el constructor con alias sindispositivos tiene un proyecto sin estructura
    When abre el dashboard del constructor
    Then la distribución no tendrá tarjetas de tipos de dispositivos
