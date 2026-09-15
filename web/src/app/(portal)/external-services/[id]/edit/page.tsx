import { ServiceForm } from "@/features/external-services/service-form";
export default async function EditExternalServicePage({params}:{params:Promise<{id:string}>}){const {id}=await params;return <ServiceForm id={id}/>}
