Feature: US03 Edición de Información del Perfil
  Como usuario
  quiero poder editar alguna parte de mi información, como mi email, número de teléfono o dirección
  para mantener mis datos actualizados.

  Scenario Outline: Edición exitosa de información de contacto
    Given el <usuario> se encuentra en la sección de edición de su perfil
    And hace clic en el botón "Guardar Cambios"
    When modifica el teléfono por <nuevo_telefono> y la dirección por <nueva_direccion>
    Then el sistema actualizará los datos satisfactoriamente
    And mostrará el mensaje de confirmación <mensaje_exito>
    Examples:
      | usuario | nuevo_telefono | nueva_direccion | mensaje_exito |
      | axel@iobuild.pe | 999888777 | Av. Primavera 123 | Perfil actualizado correctamente |
      | mateo@iobuild.pe | 988777666 | Calle Los Fresnos 456 | Perfil actualizado correctamente |

  Scenario Outline: Validación de formato incorrecto en datos de contacto
    Given el <usuario> se encuentra editando su información personal
    And hace clic en el botón "Guardar Cambios"
    When ingresa un formato inválido de teléfono <telefono_invalido>
    Then el sistema bloqueará la actualización
    And mostrará la validación de error <mensaje_error>
    Examples:
      | usuario | telefono_invalido | mensaje_error |
      | axel@iobuild.pe | 123 | El número de teléfono debe contener 9 dígitos |
