[<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
module FSharp.Configuration.TypeProviders.Impl

open ProviderImplementation.ProvidedTypes
open Microsoft.Extensions.Configuration
open FSharp.Quotations


let getProviderType<'T when 'T :> FileConfigurationSource and 'T: (new: unit -> 'T)>(assembly, nameSpace, resolutionFolder) = 

    let providerType = ProvidedTypeDefinition(assembly, nameSpace, "JsonConfiguration", Some typeof<obj>, hideObjectMethods = true, isErased = false)

    providerType.DefineStaticParameters(
        parameters = [ ProvidedStaticParameter("Path", typeof<string>) ],             
        instantiationFunction = (fun typeName args ->
            let configRoot = 
                ConfigurationBuilder()
                    .SetBasePath(resolutionFolder)
                    .Add<'T>(fun x -> x.Path <- string args.[0])
                    //.AddJsonFile(path = string args.[0])
                    .Build()
            
            let tempAssembly = ProvidedAssembly()
            let rootType = ProvidedTypeDefinition(tempAssembly, nameSpace, typeName, Some typeof<obj>, isErased = false)
            tempAssembly.AddTypes [ rootType ]

            let addInstanceTypePrefix (pt: ProvidedTypeDefinition) = 
                let intanceType = ProvidedTypeDefinition("InstanceType", Some typeof<obj>, isErased = false)
                intanceType.AddMember <| ProvidedConstructor([], (fun _ -> <@@ () @@>), IsImplicitConstructor = true) 
                pt.AddMember intanceType
                intanceType

            let intanceType = addInstanceTypePrefix rootType

            let rec addChildSectionTypes (parentConfigSection: IConfiguration) (parentStaticType: ProvidedTypeDefinition) (parentInstanceType: ProvidedTypeDefinition) = 
                for section in parentConfigSection.GetChildren() do
                    let sectionStaticType = ProvidedTypeDefinition(section.Key, Some typeof<obj>, isErased = false)
                    sectionStaticType.AddMember <| ProvidedField.Literal("Path", typeof<string>, section.Path) 
                    parentStaticType.AddMember sectionStaticType

                    let propType = 
                        if section.Value <> null 
                        then 
                            sectionStaticType.AddMember <| ProvidedField.Literal("Value", typeof<string>, section.Value) 
                            typeof<string> 
                        else    
                            let instanceType = addInstanceTypePrefix sectionStaticType
                            addChildSectionTypes section sectionStaticType instanceType
                            upcast instanceType

                    let backingField = ProvidedField(section.Key, propType)
                    parentInstanceType.AddMember backingField
                                
                    parentInstanceType.AddMember <| 
                        ProvidedProperty(
                            section.Key, 
                            propType, 
                            getterCode = (fun args -> Expr.FieldGet(args.[0], backingField)), 
                            setterCode = (fun args -> Expr.FieldSet(args.[0], backingField, args.[1]))
                        )


            addChildSectionTypes configRoot rootType intanceType
            

            rootType
        ) 
    )

    providerType 
