@US04
Feature: US04 Ver lista de proyectos
  Como constructor
  quiero ver una lista de mis proyectos
  para conocer sus unidades y abrir sus detalles.

  Scenario Outline: Constructor visualiza proyectos asociados
    Given el constructor con alias <alias> tiene el proyecto "<nombre_proyecto>" con <total_unidades> unidades
    When abre la lista de proyectos
    Then una tarjeta mostrará "<nombre_proyecto>" y <total_unidades> unidades
    And la tarjeta mostrará un estado
    Examples:
      | alias | nombre_proyecto | total_unidades |
      | sky | Torre Sky | 40 |
      | park | Residencial Park | 18 |

  Scenario: Constructor sin proyectos visualiza el estado vacío
    Given el constructor con alias nuevo no tiene proyectos
    When abre la lista de proyectos
    Then verá el estado vacío de proyectos
    And tendrá disponible la acción de crear un proyecto
