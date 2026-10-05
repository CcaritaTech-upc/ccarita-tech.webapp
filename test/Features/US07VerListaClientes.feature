@US07
Feature: US07 Ver Lista de Clientes
  Como arquitecto
  quiero ver mis clientes asociados a proyectos
  para gestionar sus datos y estado de cuenta.

  Scenario Outline: Visualización de clientes asociados al constructor
    Given el constructor con alias <alias> tiene el cliente "<nombre_cliente>" en el proyecto "<proyecto>"
    When abre la sección de clientes
    Then la lista mostrará "<nombre_cliente>" asociado a "<proyecto>"
    And mostrará el estado de cuenta "<estado_cuenta>" y la acción de ver perfil
    Examples:
      | alias | nombre_cliente | proyecto | estado_cuenta |
      | clientesa | Alex Resident | North Tower | Activo |
      | clientesb | Carla Flores | Edificio Panorama | Activo |

  Scenario: Constructor sin clientes visualiza la lista vacía
    Given el constructor con alias sinclientes no tiene clientes
    When abre la sección de clientes
    Then la lista de clientes estará vacía
