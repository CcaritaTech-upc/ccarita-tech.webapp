Feature: US04 Ver lista de proyectos
  Como ingeniero
  quiero ver una lista de todos mis proyectos
  para poder conocer el estado y detalles de cada uno.

  Scenario Outline: Constructor con proyectos registrados visualiza su catálogo
    Given que el constructor <builder_email> tiene proyectos asociados en el sistema
    When accede a la vista principal de "Proyectos"
    Then el sistema mostrará una tarjeta para cada proyecto incluyendo nombre <nombre_proyecto>, unidades <total_unidades> y estado <estado>
    And las opciones de gestión quedarán habilitadas
    Examples:
      | builder_email | nombre_proyecto | total_unidades | estado |
      | builder@iobuild.pe | Torre Sky | 40 | Active |
      | builder@iobuild.pe | Residencial Park | 18 | Draft |

  Scenario Outline: Constructor sin proyectos registrados
    Given que el constructor <builder_email> acaba de registrarse y no tiene proyectos
    When accede a la vista de "Proyectos"
    Then el sistema mostrará el estado vacío con el mensaje <mensaje_vacio>
    And presentará el botón destacado "Crear mi primer proyecto"
    Examples:
      | builder_email | mensaje_vacio |
      | nuevo@iobuild.pe | Aún no tienes proyectos registrados |
